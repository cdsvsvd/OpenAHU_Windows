#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""OpenAPI 契约漂移检查：把 docs/openapi.json 与 Contracts 源码、已注册路由三方比对。

用法（仓库根目录）：
    python src/OpenAHU.Api/tools/check_openapi_contract.py
    python src/OpenAHU.Api/tools/check_openapi_contract.py --fail-on-warn   # 严格模式（CI 收紧时用）

硬性检查（不通过即退出码 1）：
  1. spec 里的「方法 + 路径」集合与源码注册的路由完全一致；
  2. spec 引用的每个 schema 都必须存在，且能对应到一个 Contracts record；
  3. schema 的字段名集合、类型种类、可空性必须与 record 一致；
  4. 每个操作必须声明该方法的成功码（GET/PUT=200、POST=201、DELETE=204），以及 401/500/503。

软性检查（默认只告警，`--fail-on-warn` 时升级为失败）：
  5. 非可空字段应出现在 `required` 里（否则生成器会产出全可空模型）；
  6. 错误码不应超出该操作类型的策略范围（例如 GET 声明 409）；
  7. POST 201 应声明 `Location` 响应头。

本脚本不启动服务、不编译 C#，是文本级护栏；`format`（uri/date/date-time）只用于归一化，不做强校验。
"""

from __future__ import annotations

import argparse
import json
import re
import sys
from pathlib import Path

MAP_ENDPOINT = re.compile(r'Map(Get|Post|Put|Delete)\(\s*"([^"]+)"')
MAP_HELPER = re.compile(r'Map\w+\(\s*\w+\s*,\s*"([A-Z]+)"\s*,\s*"([^"]+)"')
RECORD = re.compile(r"public\s+sealed\s+record\s+(\w+)\s*\(([^;{]*?)\)\s*;", re.S)
GROUP_PREFIX = "/api/v1"

# 方法 -> (成功码, 允许的全部状态码)
CODE_POLICY: dict[str, tuple[int, set[int]]] = {
    "get": (200, {200, 400, 401, 404, 500, 503}),
    "post": (201, {201, 400, 401, 404, 409, 500, 503}),
    "put": (200, {200, 400, 401, 404, 409, 500, 503}),
    "delete": (204, {204, 400, 401, 404, 409, 500, 503}),
}
REQUIRED_CODES = {401, 500, 503}

# C# 参数类型 -> (种类, 是否可空)
TYPE_MAP = {
    "string": ("string", False),
    "int": ("integer", False),
    "decimal": ("number", False),
    "bool": ("boolean", False),
    "DateOnly": ("date", False),
    "DateTimeOffset": ("datetime", False),
    "DayOfWeek": ("integer", False),
    "IReadOnlyList<int>": ("array<integer>", False),
}


def camel(name: str) -> str:
    return name[:1].lower() + name[1:]


def parse_param(text: str) -> tuple[str, str, bool]:
    """'string? College' -> ('college', 'string', True)"""
    parts = re.sub(r"\s+", " ", text.strip()).rsplit(" ", 1)
    if len(parts) != 2:
        raise ValueError(f"无法解析的参数声明：{text!r}")
    raw_type, name = parts
    raw_type = raw_type.replace("System.", "")
    nullable = raw_type.endswith("?")
    base = raw_type[:-1] if nullable else raw_type
    if base not in TYPE_MAP:
        raise ValueError(f"未登记的类型：{base}（参数 {name}）")
    kind, base_nullable = TYPE_MAP[base]
    return camel(name), kind, nullable or base_nullable


def parse_records(contracts_dir: Path) -> dict[str, list[tuple[str, str, bool]]]:
    records: dict[str, list[tuple[str, str, bool]]] = {}
    for path in sorted(contracts_dir.rglob("*.cs")):
        text = path.read_text(encoding="utf-8-sig")
        for match in RECORD.finditer(text):
            name = match.group(1)
            params = [parse_param(p) for p in match.group(2).split(",") if p.strip()]
            records[name] = params
    return records


def source_routes(api_dir: Path) -> set[tuple[str, str]]:
    routes: set[tuple[str, str]] = set()
    for path in sorted(api_dir.rglob("*.cs")):
        text = path.read_text(encoding="utf-8-sig")
        for match in MAP_ENDPOINT.finditer(text):
            routes.add((match.group(1).upper(), GROUP_PREFIX + match.group(2)))
        for match in MAP_HELPER.finditer(text):
            routes.add((match.group(1), GROUP_PREFIX + match.group(2)))
    return routes


def spec_kind(prop: dict) -> tuple[str, bool]:
    """把 OpenAPI 属性还原成 (种类, 是否可空)。"""
    if "$ref" in prop:
        return prop["$ref"].rsplit("/", 1)[-1], False
    types = prop.get("type")
    nullable = False
    if isinstance(types, list):
        nullable = "null" in types
        types = [t for t in types if t != "null"]
        types = types[0] if types else "string"
    if types == "array":
        items = prop.get("items", {})
        inner, _ = spec_kind(items)
        return f"array<{inner}>", nullable
    if types == "string":
        fmt = prop.get("format")
        if fmt == "date-time":
            return "datetime", nullable
        if fmt == "date":
            return "date", nullable
        return "string", nullable
    return str(types), nullable


def main() -> int:
    root = Path(__file__).resolve().parents[3]
    parser = argparse.ArgumentParser()
    parser.add_argument("--spec", default=str(root / "docs" / "openapi.json"))
    parser.add_argument("--contracts", default=str(root / "src" / "OpenAHU.Contracts"))
    parser.add_argument("--api", default=str(root / "src" / "OpenAHU.Api"))
    parser.add_argument(
        "--fail-on-warn",
        action="store_true",
        help="把 required / 错误码策略 / Location 头的告警升级为失败",
    )
    args = parser.parse_args()

    spec_path = Path(args.spec)
    if not spec_path.is_file():
        print(f"FAIL 找不到 OpenAPI 文档：{spec_path}")
        return 1
    spec = json.loads(spec_path.read_text(encoding="utf-8-sig"))
    records = parse_records(Path(args.contracts))
    routes = source_routes(Path(args.api))

    problems: list[str] = []
    warnings: list[str] = []

    paths = spec.get("paths", {})
    schemas = spec.get("components", {}).get("schemas", {})

    # 1) 路由 <-> spec 操作集合
    spec_ops: set[tuple[str, str]] = set()
    for path, ops in paths.items():
        for method in ops:
            if method.lower() in CODE_POLICY:
                spec_ops.add((method.upper(), path))
    missing = sorted(routes - spec_ops)
    extra = sorted(spec_ops - routes)
    for method, path in missing:
        problems.append(f"源码已注册但 spec 缺失：{method} {path}")
    for method, path in extra:
        problems.append(f"spec 声明但源码未注册：{method} {path}")

    # 2/3) schema 引用与字段一致性
    used_refs: set[str] = set()

    def collect(node) -> None:
        if isinstance(node, dict):
            if "$ref" in node:
                used_refs.add(node["$ref"].rsplit("/", 1)[-1])
            for value in node.values():
                collect(value)
        elif isinstance(node, list):
            for value in node:
                collect(value)

    collect(paths)
    for name in sorted(used_refs):
        if name not in schemas:
            problems.append(f"spec 引用了不存在的 schema：{name}")
        elif name not in records:
            problems.append(f"schema {name} 在 Contracts 中没有对应的 sealed record")

    for name, props in sorted(schemas.items()):
        if name not in records:
            problems.append(f"schema {name} 没有对应的 Contracts record（多余定义）")
            continue
        record_fields = {f[0]: (f[1], f[2]) for f in records[name]}
        spec_fields = props.get("properties", {})
        for field in sorted(set(record_fields) - set(spec_fields)):
            problems.append(f"{name}.{field}：record 有该字段，spec 缺失")
        for field in sorted(set(spec_fields) - set(record_fields)):
            problems.append(f"{name}.{field}：spec 多出字段，record 中不存在")
        for field in sorted(set(record_fields) & set(spec_fields)):
            rec_kind, rec_nullable = record_fields[field]
            spec_k, spec_nullable = spec_kind(spec_fields[field])
            if spec_k in ("string",) and rec_kind == "string":
                pass
            elif spec_k != rec_kind:
                problems.append(
                    f"{name}.{field}：类型不一致（record={rec_kind} spec={spec_k}）"
                )
            if rec_nullable != spec_nullable:
                problems.append(
                    f"{name}.{field}：可空性不一致（record={'可空' if rec_nullable else '非空'} "
                    f"spec={'可空' if spec_nullable else '非空'}）"
                )
        required = set(props.get("required", []))
        non_nullable = sorted(f for f, (_, nullable) in record_fields.items() if not nullable)
        absent = [f for f in non_nullable if f not in required]
        if absent:
            warnings.append(f"{name}：非空字段未列入 required → {', '.join(absent)}")

    # 4/6/7) 每个操作的状态码与响应头
    for path, ops in sorted(paths.items()):
        for method, op in sorted(ops.items()):
            if method.lower() not in CODE_POLICY:
                continue
            success, allowed = CODE_POLICY[method.lower()]
            declared = {int(c) for c in op.get("responses", {}) if str(c).isdigit()}
            if success not in declared:
                problems.append(f"{method.upper()} {path}：缺少成功码 {success}")
            for code in sorted(REQUIRED_CODES - declared):
                problems.append(f"{method.upper()} {path}：缺少必要错误码 {code}")
            over = sorted(declared - allowed)
            if over:
                warnings.append(
                    f"{method.upper()} {path}：声明了该操作不该返回的状态码 {over}"
                )
            if method.lower() == "post" and 201 in declared:
                headers = op["responses"]["201"].get("headers", {})
                if "Location" not in headers:
                    warnings.append(f"POST {path}：201 未声明 Location 响应头")

    # 输出
    print(f"spec    : {spec_path}")
    print(f"contracts: {args.contracts}")
    print(f"api     : {args.api}")
    print(f"spec 操作 {len(spec_ops)} 个 / 源码路由 {len(routes)} 个 / schema {len(schemas)} 个 / "
          f"Contracts record {len(records)} 个")
    for method, path in sorted(spec_ops):
        print(f"  [OK] {method:6s} {path}")

    if warnings:
        print("\n告警（默认不失败，可 --fail-on-warn 收紧）：")
        for item in warnings:
            print("  ! " + item)

    if problems:
        print("\n失败项：")
        for item in problems:
            print("  - " + item)
        print(f"\n结果：契约漂移（{len(problems)} 项硬失败）")
        return 1

    if warnings and args.fail_on_warn:
        print(f"\n结果：--fail-on-warn 模式下有 {len(warnings)} 项告警，视为失败")
        return 1

    print("\n结果：openapi 与 Contracts、路由三方一致"
          + (f"（另有 {len(warnings)} 项告警）" if warnings else ""))
    print("注意：本检查不编译 C#、不启动服务。")
    return 0


if __name__ == "__main__":
    # GitHub Actions 的 Windows 控制台可能默认 cp1252；强制 UTF-8 输出。
    for stream in (sys.stdout, sys.stderr):
        if stream is not None and hasattr(stream, "reconfigure"):
            stream.reconfigure(encoding="utf-8")
    sys.exit(main())
