#!/usr/bin/env python3
"""Importa somente arte dos ZIPs locais, preservando GUIDs e sem baixar arquivos."""
from pathlib import Path, PurePosixPath
from zipfile import ZipFile
import json
import shutil
import tarfile

ROOT = Path(__file__).resolve().parents[1]
DEST = ROOT / "Assets/_Project/Art/Models/Characters"


def keep(model, path):
    p = path.lower()
    ext = PurePosixPath(p).suffix
    if model == "Gravemind":
        return "/shaders/" not in p and ext in {".fbx", ".mat", ".png", ".bmp", ".anim", ".controller", ".prefab"}
    if model == "Biomechanical":
        return (p.endswith("/sk_bio_mutant.fbx") or
                p.endswith("/biomech_mutant_skin_1.prefab") or
                ("/materials/" in p and PurePosixPath(p).name in {"m_bm_skin_1_top.mat", "m_bm_skin_1_bottom.mat"}) or
                ("/textures/skin_1/" in p and "/bloody/" not in p and p.endswith("_a.png")))
    if model == "Survivalist":
        return ("/basemesh/" in p and ext == ".fbx" or
                "/materials/" in p and ext == ".mat" or
                "/texture/" in p and ext == ".png" and ("color" in p or "albedo" in p) or
                "/character/animations/" in p and ext == ".fbx" or
                p.endswith("/prefab/sk_military_survivalist (1).prefab") or
                p.endswith("/starterassets/license.txt"))
    if model == "AdventureFemale":
        return (p.endswith("/mesh/adventures_woman.fbx") or
                p.endswith("/prefabs/adventures_woman_01.prefab") or
                ("/materials/" in p and PurePosixPath(p).name in {"adventures_woman_01.mat", "adventures_woman_hair_01.mat"}) or
                ("/textures/" in p and p.endswith("albedo_01.tga") and "/pistol/" not in p))
    return False


def import_package(model, archive):
    target = DEST / model / "Source"
    target.mkdir(parents=True, exist_ok=True)
    with ZipFile(ROOT / "modelo-jogo" / archive) as z:
        package = next(n for n in z.namelist() if n.lower().endswith(".unitypackage"))
        paths = {}
        with z.open(package) as stream, tarfile.open(fileobj=stream, mode="r|gz") as tar:
            for member in tar:
                if member.isfile() and member.name.endswith("/pathname"):
                    original = tar.extractfile(member).read().decode("utf-8").strip()
                    relative = PurePosixPath(original)
                    if relative.is_absolute() or ".." in relative.parts or relative.parts[0] != "Assets":
                        raise ValueError(f"Caminho inesperado: {original}")
                    if keep(model, original):
                        paths[member.name.split("/")[0]] = relative
        count = 0
        with z.open(package) as stream, tarfile.open(fileobj=stream, mode="r|gz") as tar:
            for member in tar:
                parts = member.name.split("/")
                if not member.isfile() or len(parts) != 2 or parts[0] not in paths:
                    continue
                if parts[1] not in {"asset", "asset.meta"}:
                    continue
                relative = paths[parts[0]]
                output = target.joinpath(*relative.parts[1:])
                if parts[1] == "asset.meta":
                    output = output.with_name(output.name + ".meta")
                output.parent.mkdir(parents=True, exist_ok=True)
                # Não sobrescreve adaptações feitas posteriormente na Unity.
                if output.exists():
                    continue
                with output.open("wb") as f:
                    shutil.copyfileobj(tar.extractfile(member), f)
                count += 1
        report = {
            "model": model, "archive": archive, "package": package,
            "files": [str(p) for p in paths.values()],
            "note": "Arte importada sem scripts, cenas ou controladores de gameplay dos fornecedores. ZIP original preservado. GUIDs originais mantidos."
        }
        report_path = ROOT / "modelo-jogo" / f"{model}-import.json"
        report_path.write_text(json.dumps(report, ensure_ascii=False, indent=2) + "\n")
        print(f"{model}: {len(paths)} assets selecionados; {count} arquivos gravados.", flush=True)


if __name__ == "__main__":
    for model, archive in [
        ("Gravemind", "gravemind dreadnought.zip"),
        ("Biomechanical", "biomechanical mutant.zip"),
        ("Survivalist", "survivalist character.zip"),
        ("AdventureFemale", "adventure female free.zip"),
    ]:
        import_package(model, archive)
