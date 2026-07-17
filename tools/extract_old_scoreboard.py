"""Extract old Block N Load scoreboard and lobby UI data from the legacy Scenes bundle.

This script focuses on the scene-embedded UI hierarchy rather than standalone bundle
entries. It emits a compact JSON report with:
  - named roots such as Scores, Lobby, Voting, BottomPanel, PlayerInfo
  - child hierarchy for those roots
  - attached MonoBehaviour script names for each GameObject
  - named assets whose names indicate score/lobby/team/chat/map UI relevance
"""

from __future__ import annotations

import argparse
import json
import re
from collections import Counter
from pathlib import Path
from typing import Any

import UnityPy


INTERESTING_ROOTS = {
    "Scores",
    "Lobby",
    "Voting",
    "BottomPanel",
    "PlayerInfo",
}

ASSET_NAME_PATTERN = re.compile(
    r"(score|result|team|lobby|chat|vote|player|hero|leader|rank|tab|map)",
    re.IGNORECASE,
)


def read_typetree_safe(obj: Any) -> dict[str, Any] | None:
    try:
        return obj.read_typetree()
    except Exception:
        return None


ObjectKey = tuple[str, int]


def get_object_key(obj: Any) -> ObjectKey:
    return (obj.assets_file.name, obj.path_id)


def build_indices(env: Any) -> tuple[dict[ObjectKey, Any], dict[ObjectKey, dict[str, Any]], dict[ObjectKey, str], Counter]:
    objects_by_id: dict[ObjectKey, Any] = {}
    trees_by_id: dict[ObjectKey, dict[str, Any]] = {}
    names_by_id: dict[ObjectKey, str] = {}
    type_counts: Counter = Counter()

    for obj in env.objects:
        key = get_object_key(obj)
        objects_by_id[key] = obj
        type_counts[obj.type.name] += 1
        tree = read_typetree_safe(obj)
        if tree is not None:
            trees_by_id[key] = tree
            name = tree.get("m_Name")
            if isinstance(name, str) and name:
                names_by_id[key] = name

    return objects_by_id, trees_by_id, names_by_id, type_counts


def get_game_object_name_from_transform(
    file_name: str,
    transform_path_id: int,
    trees_by_id: dict[ObjectKey, dict[str, Any]],
) -> str | None:
    transform_tree = trees_by_id.get((file_name, transform_path_id))
    if not transform_tree:
        return None

    go_ptr = transform_tree.get("m_GameObject")
    if not isinstance(go_ptr, dict):
        return None

    go_tree = trees_by_id.get((file_name, go_ptr.get("m_PathID")))
    if not go_tree:
        return None

    name = go_tree.get("m_Name")
    return name if isinstance(name, str) and name else None


def get_transform_path_id(game_object_tree: dict[str, Any]) -> int | None:
    for class_id, ptr in game_object_tree.get("m_Component", []):
        if class_id == 4 and isinstance(ptr, dict):
            return ptr.get("m_PathID")
    return None


def get_component_script_name(
    component_key: ObjectKey,
    objects_by_id: dict[ObjectKey, Any],
    trees_by_id: dict[ObjectKey, dict[str, Any]],
) -> str | None:
    component_obj = objects_by_id.get(component_key)
    if component_obj is None or component_obj.type.name != "MonoBehaviour":
        return None

    try:
        component = component_obj.read()
        if not getattr(component, "m_Script", None):
            return None
        script = component.m_Script.read()
    except Exception:
        return None

    for attr in ("m_ClassName", "m_Name", "name"):
        value = getattr(script, attr, None)
        if isinstance(value, str) and value:
            return value

    component_tree = trees_by_id.get(component_key)
    if not component_tree:
        return None
    script_ptr = component_tree.get("m_Script")
    if not isinstance(script_ptr, dict):
        return None
    script_tree = trees_by_id.get((component_key[0], script_ptr.get("m_PathID")))
    if not script_tree:
        return None
    value = script_tree.get("m_Name")
    return value if isinstance(value, str) and value else None


def get_game_object_components(
    file_name: str,
    game_object_tree: dict[str, Any],
    objects_by_id: dict[ObjectKey, Any],
    trees_by_id: dict[ObjectKey, dict[str, Any]],
) -> list[dict[str, Any]]:
    components: list[dict[str, Any]] = []
    for class_id, ptr in game_object_tree.get("m_Component", []):
        if not isinstance(ptr, dict):
            continue
        path_id = ptr.get("m_PathID")
        component_key = (file_name, path_id)
        component_obj = objects_by_id.get(component_key)
        if component_obj is None:
            continue

        item = {
            "path_id": path_id,
            "file": file_name,
            "type": component_obj.type.name,
        }
        script_name = get_component_script_name(component_key, objects_by_id, trees_by_id)
        if script_name:
            item["script"] = script_name
        components.append(item)
    return components


def build_hierarchy_node(
    file_name: str,
    transform_path_id: int,
    depth: int,
    max_depth: int,
    objects_by_id: dict[ObjectKey, Any],
    trees_by_id: dict[ObjectKey, dict[str, Any]],
) -> dict[str, Any]:
    transform_tree = trees_by_id[(file_name, transform_path_id)]
    go_ptr = transform_tree["m_GameObject"]
    go_path_id = go_ptr["m_PathID"]
    go_tree = trees_by_id[(file_name, go_path_id)]

    node = {
        "name": go_tree.get("m_Name") or "",
        "path_id": go_path_id,
        "file": file_name,
        "active": bool(go_tree.get("m_IsActive", False)),
        "components": get_game_object_components(file_name, go_tree, objects_by_id, trees_by_id),
        "children": [],
    }

    if depth >= max_depth:
        return node

    for child_ptr in transform_tree.get("m_Children", []):
        child_transform_path_id = child_ptr.get("m_PathID")
        if (file_name, child_transform_path_id) not in trees_by_id:
            continue
        node["children"].append(
            build_hierarchy_node(
                file_name,
                child_transform_path_id,
                depth + 1,
                max_depth,
                objects_by_id,
                trees_by_id,
            )
        )

    return node


def collect_root_hierarchies(
    trees_by_id: dict[ObjectKey, dict[str, Any]],
    objects_by_id: dict[ObjectKey, Any],
    names_by_id: dict[ObjectKey, str],
    max_depth: int,
) -> list[dict[str, Any]]:
    roots: list[dict[str, Any]] = []

    for object_key, name in names_by_id.items():
        obj = objects_by_id.get(object_key)
        if obj is None or obj.type.name != "GameObject":
            continue
        if name not in INTERESTING_ROOTS:
            continue

        file_name, path_id = object_key
        game_object_tree = trees_by_id[object_key]
        transform_path_id = get_transform_path_id(game_object_tree)
        if transform_path_id is None or (file_name, transform_path_id) not in trees_by_id:
            continue

        roots.append(
            {
                "root_name": name,
                "path_id": path_id,
                "file": file_name,
                "hierarchy": build_hierarchy_node(
                    file_name,
                    transform_path_id,
                    depth=0,
                    max_depth=max_depth,
                    objects_by_id=objects_by_id,
                    trees_by_id=trees_by_id,
                ),
            }
        )

    roots.sort(key=lambda item: (item["root_name"], item["file"], item["path_id"]))
    return roots


def collect_interesting_assets(names_by_id: dict[ObjectKey, str], objects_by_id: dict[ObjectKey, Any]) -> list[dict[str, Any]]:
    assets: list[dict[str, Any]] = []
    for object_key, name in names_by_id.items():
        file_name, path_id = object_key
        obj = objects_by_id[object_key]
        if not ASSET_NAME_PATTERN.search(name):
            continue
        assets.append(
            {
                "path_id": path_id,
                "file": file_name,
                "type": obj.type.name,
                "name": name,
            }
        )

    assets.sort(key=lambda item: (item["type"], item["name"], item["file"], item["path_id"]))
    return assets


def collect_script_bindings(
    trees_by_id: dict[ObjectKey, dict[str, Any]],
    objects_by_id: dict[ObjectKey, Any],
    names_by_id: dict[ObjectKey, str],
) -> list[dict[str, Any]]:
    bindings: list[dict[str, Any]] = []
    for object_key, obj in objects_by_id.items():
        if obj.type.name != "MonoBehaviour":
            continue
        tree = trees_by_id.get(object_key)
        if not tree:
            continue

        script_name = get_component_script_name(object_key, objects_by_id, trees_by_id)
        if not script_name:
            continue

        go_ptr = tree.get("m_GameObject")
        if not isinstance(go_ptr, dict):
            continue
        file_name, path_id = object_key
        go_name = names_by_id.get((file_name, go_ptr.get("m_PathID")), "")

        if not ASSET_NAME_PATTERN.search(script_name) and not ASSET_NAME_PATTERN.search(go_name):
            continue

        bindings.append(
            {
                "path_id": path_id,
                "file": file_name,
                "script": script_name,
                "game_object": go_name,
            }
        )

    bindings.sort(key=lambda item: (item["script"], item["game_object"], item["file"], item["path_id"]))
    return bindings


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("bundle_path", help="Path to the old Scenes asset bundle")
    parser.add_argument(
        "--output",
        default=str(Path("artifacts") / "old-scoreboard-report.json"),
        help="Where to write the JSON report",
    )
    parser.add_argument(
        "--max-depth",
        type=int,
        default=4,
        help="Hierarchy depth to emit for selected roots",
    )
    args = parser.parse_args()

    env = UnityPy.load(args.bundle_path)
    objects_by_id, trees_by_id, names_by_id, type_counts = build_indices(env)

    report = {
        "bundle_path": str(Path(args.bundle_path)),
        "file_count": len(env.files),
        "type_counts": dict(type_counts.most_common()),
        "interesting_roots": collect_root_hierarchies(
            trees_by_id=trees_by_id,
            objects_by_id=objects_by_id,
            names_by_id=names_by_id,
            max_depth=args.max_depth,
        ),
        "script_bindings": collect_script_bindings(
            trees_by_id=trees_by_id,
            objects_by_id=objects_by_id,
            names_by_id=names_by_id,
        ),
        "interesting_assets": collect_interesting_assets(
            names_by_id=names_by_id,
            objects_by_id=objects_by_id,
        ),
    }

    output_path = Path(args.output)
    output_path.parent.mkdir(parents=True, exist_ok=True)
    output_path.write_text(json.dumps(report, indent=2), encoding="utf-8")
    print(f"Wrote {output_path}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
