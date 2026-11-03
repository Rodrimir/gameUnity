#!/usr/bin/env bash
# Move para Legacy/ os arquivos ignorados pelo Git que ficaram nos caminhos antigos depois do
# checkout da branch rebuild/gamekit (o Git só move arquivos versionados). Sem isso, a Unity
# importaria de novo os modelos e o kit medieval do projeto antigo.
# Uso: Tools/move_legacy_ignored.sh [--dry-run]
set -euo pipefail

PROJECT_DIR="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")/.." && pwd -P)"
DRY_RUN=0
[[ "${1:-}" == --dry-run ]] && DRY_RUN=1
MOVED=0

move_path() {
  local source="$1" target="$2"
  if [[ -e "$target" ]]; then
    echo "Já existe, mantido no lugar: ${source#"$PROJECT_DIR"/} (destino ${target#"$PROJECT_DIR"/})" >&2
    return
  fi
  echo "${source#"$PROJECT_DIR"/} -> ${target#"$PROJECT_DIR"/}"
  MOVED=$((MOVED + 1))
  if (( DRY_RUN == 0 )); then
    mkdir -p "$(dirname "$target")"
    mv "$source" "$target"
  fi
}

# Modelos e texturas de origem dos personagens antigos.
CHARACTERS="$PROJECT_DIR/Assets/_Project/Art/Models/Characters"
if [[ -d "$CHARACTERS" ]]; then
  while IFS= read -r -d '' file; do
    move_path "$file" "$PROJECT_DIR/Legacy/_Project/${file#"$PROJECT_DIR/Assets/_Project/"}"
  done < <(find "$CHARACTERS" -path '*/Source/*' -type f \
    \( -iname '*.fbx' -o -iname '*.png' -o -iname '*.tga' -o -iname '*.bmp' \) -print0)
  if (( DRY_RUN == 0 )); then
    # Remove só pastas vazias que sobraram do projeto antigo.
    find "$PROJECT_DIR/Assets/_Project/Art" -depth -type d -empty -delete 2>/dev/null || true
  fi
fi

# Pacote local do Medieval Village Kit: em Packages/ a Unity o carregaria automaticamente.
MEDIEVAL_KIT="$PROJECT_DIR/Packages/com.danielgruginski.medievalkit"
if [[ -d "$MEDIEVAL_KIT" ]]; then
  move_path "$MEDIEVAL_KIT" "$PROJECT_DIR/Legacy/Packages/com.danielgruginski.medievalkit"
fi

if (( MOVED == 0 )); then
  echo "Nada a mover: nenhum arquivo ignorado do projeto antigo nos caminhos antigos."
elif (( DRY_RUN == 1 )); then
  echo "$MOVED item(ns) seriam movidos (simulação; rode sem --dry-run para mover)."
else
  echo "$MOVED item(ns) movidos para Legacy/."
fi
