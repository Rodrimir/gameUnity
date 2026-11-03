#!/usr/bin/env bash
set -euo pipefail

PROJECT_DIR="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")/.." && pwd -P)"
LOCAL_DIR="$PROJECT_DIR/.unity"
PROJECT_VERSION="$(awk '/^m_EditorVersion:/ { print $2 }' "$PROJECT_DIR/ProjectSettings/ProjectVersion.txt" 2>/dev/null || true)"

# Editor (somente leitura): variável explícita, depois as instalações conhecidas da equipe.
EDITOR_CANDIDATES=(
  "${GAMEYUNIT_UNITY_EDITOR:-}"
  "/mnt/SSD/Unity/editors/$PROJECT_VERSION/Editor/Unity"
  "$HOME/Unity/Hub/Editor/$PROJECT_VERSION/Editor/Unity"
)
EDITOR_BIN=""
for CANDIDATE in "${EDITOR_CANDIDATES[@]}"; do
  if [[ -n "$CANDIDATE" && -x "$CANDIDATE" ]]; then EDITOR_BIN="$CANDIDATE"; break; fi
done
EDITOR_BIN="${EDITOR_BIN:-${GAMEYUNIT_UNITY_EDITOR:-$HOME/Unity/Hub/Editor/$PROJECT_VERSION/Editor/Unity}}"

# Licença ativada pelo Unity Hub, copiada para dentro do projeto (nunca versionada).
LICENSE_SOURCES=(
  /mnt/SSD/Unity/xdg/config/unity3d/Unity
  "$HOME/.config/unity3d/Unity"
)

# Os comandos de janela dependem do wmctrl; sem ele, falha com instrução em vez de erro cru.
if ! command -v wmctrl >/dev/null 2>&1 && [[ "${1:-}" != --status ]]; then
  wmctrl() {
    echo "wmctrl não instalado: instale com 'sudo pacman -S wmctrl' ou feche/abra a Unity manualmente." >&2
    exit 1
  }
fi

if [[ "${1:-}" == --print-paths ]]; then
  echo "Projeto:            $PROJECT_DIR"
  echo "Versão do projeto:  ${PROJECT_VERSION:-desconhecida}"
  echo "Editor:             $EDITOR_BIN $([[ -x "$EDITOR_BIN" ]] && echo '(ok)' || echo '(NÃO encontrado)')"
  echo "Licença local:      $LOCAL_DIR/config/unity3d/Unity/licenses $([[ -d "$LOCAL_DIR/config/unity3d/Unity/licenses" ]] && echo '(ok)' || echo '(será copiada na próxima execução)')"
  for SOURCE in "${LICENSE_SOURCES[@]}"; do
    [[ -d "$SOURCE/licenses" ]] && echo "Origem da licença:  $SOURCE/licenses"
  done
  echo "Cache UPM:          $LOCAL_DIR/upm"
  echo "Cache Asset Store:  $LOCAL_DIR/asset-store"
  echo "Dados/config/cache: $LOCAL_DIR/{data,config,cache}"
  echo "Temporários:        $LOCAL_DIR/tmp"
  echo "Logs:               $PROJECT_DIR/Logs"
  echo "wmctrl:             $(type -P wmctrl || echo 'não instalado (--reopen-*, --close-editor e capturas indisponíveis)')"
  echo "Unity Hub IPC:      conexão automática quando o socket local estiver disponível"
  exit 0
fi

if [[ "${1:-}" == --status ]]; then
  ps -eo pid,etime,stat,comm | awk 'NR == 1 || tolower($4) ~ /unity|bwrap/'
  if command -v wmctrl >/dev/null 2>&1; then
    wmctrl -lpGx | awk '$8 ~ /[Uu]nity/ { print $1, "pid=" $3, $8, substr($0, index($0, $10)) }'
  else
    echo "(wmctrl não instalado: janelas não listadas)"
  fi
  fuser -v "$PROJECT_DIR/Temp/UnityLockfile" 2>&1 || true
  exit 0
fi

if [[ "${1:-}" == --focus ]]; then
  exec wmctrl -xa Unity
fi

if [[ "${1:-}" == --debug-editor ]]; then
  EDITOR_PID="$(pgrep -f "^$EDITOR_BIN -projectPath $PROJECT_DIR " | head -1)"
  if [[ -z "$EDITOR_PID" ]]; then exit 1; fi
  exec gdb -nx -batch -iex "set debuginfod enabled off" -ex "thread 1" \
    -ex "bt 20" -ex detach -p "$EDITOR_PID"
fi

if [[ "${1:-}" == --capture-window || "${1:-}" == --capture-package-manager || "${1:-}" == --capture-dialog ]]; then
  if [[ "${1:-}" == --capture-dialog ]]; then
    WINDOW_ID="$(wmctrl -lx | awk '$3 ~ /Unity-6000/ && /Warning|Import Unity Package|Project Settings|Error!/ { print $1; exit }')"
    CAPTURE_NAME=UnityDialog.png
  elif [[ "${1:-}" == --capture-package-manager ]]; then
    WINDOW_ID="$(wmctrl -lx | awk '$3 ~ /Unity-6000/ && /Package Manager/ { print $1; exit }')"
    CAPTURE_NAME=PackageManager.png
  else
    WINDOW_ID="$(wmctrl -lx | awk '$3 ~ /Unity-6000/ { print $1; exit }')"
    CAPTURE_NAME=UnityWindow.png
  fi
  if [[ -z "$WINDOW_ID" ]]; then
    echo "Janela da Unity não encontrada." >&2
    exit 1
  fi
  mkdir -p "$PROJECT_DIR/Logs"
  mkdir -p "$LOCAL_DIR/tmp"
  export MAGICK_TEMPORARY_PATH="$LOCAL_DIR/tmp"
  exec import -window "$WINDOW_ID" "$PROJECT_DIR/Logs/$CAPTURE_NAME"
fi

if [[ "${1:-}" == --capture-error ]]; then
  WINDOW_ID="$(wmctrl -lpGx | awk '$0 ~ / Error!/ { print $1; exit }')"
  if [[ -z "$WINDOW_ID" ]]; then echo "Diálogo Error! não encontrado." >&2; exit 1; fi
  mkdir -p "$PROJECT_DIR/Logs" "$LOCAL_DIR/tmp"
  export MAGICK_TEMPORARY_PATH="$LOCAL_DIR/tmp"
  exec import -window "$WINDOW_ID" "$PROJECT_DIR/Logs/UnityError.png"
fi

if [[ "${1:-}" == --dismiss-error ]]; then
  WINDOW_ID="$(wmctrl -lpGx | awk '$0 ~ / Error!/ { print $1; exit }')"
  if [[ -n "$WINDOW_ID" ]]; then wmctrl -ic "$WINDOW_ID"; fi
  exit 0
fi

if [[ "${1:-}" == --close-editor ]]; then
  WINDOW_ID="$(wmctrl -lx | awk '$3 ~ /Unity-6000/ { print $1; exit }')"
  if [[ -n "$WINDOW_ID" ]]; then
    wmctrl -ic "$WINDOW_ID"
    for ((ATTEMPT = 0; ATTEMPT < 40; ATTEMPT++)); do
      if ! wmctrl -l | awk '{ print $1 }' | grep -qx "$WINDOW_ID"; then break; fi
      sleep 0.5
    done
    if wmctrl -l | awk '{ print $1 }' | grep -qx "$WINDOW_ID"; then
      echo "A Unity não fechou; nenhuma alteração visual será aplicada." >&2
      exit 1
    fi
  fi
  for ((ATTEMPT = 0; ATTEMPT < 40; ATTEMPT++)); do
    if ! fuser "$PROJECT_DIR/Temp/UnityLockfile" >/dev/null 2>&1; then break; fi
    sleep 0.5
  done
  if fuser "$PROJECT_DIR/Temp/UnityLockfile" >/dev/null 2>&1; then
    echo "A Unity ainda mantém o lock do projeto; não será encerrada à força." >&2
    exit 1
  fi
  exit 0
fi

if [[ ! -x "$EDITOR_BIN" ]]; then
  echo "Unity não encontrada: $EDITOR_BIN" >&2
  exit 1
fi

if [[ "${1:-}" == --reopen || "${1:-}" == --reopen-game || "${1:-}" == --reopen-play || "${1:-}" == --reopen-kit-import ]]; then
  # Pontos de entrada do Editor (Assets/_Project/Scripts/Editor). Enquanto não existirem,
  # a Unity reabre na última cena, sem -executeMethod.
  case "${1:-}" in
    --reopen-game) REOPEN_METHOD=GameYunit.Editor.EditorEntryPoints.OpenGame ;;
    --reopen-play) REOPEN_METHOD=GameYunit.Editor.EditorEntryPoints.PlayGame ;;
    --reopen-kit-import) REOPEN_METHOD=GameYunit.Editor.EditorEntryPoints.OpenKitImport ;;
    *) REOPEN_METHOD="" ;;
  esac
  if [[ -n "$REOPEN_METHOD" ]] &&
    ! grep -rqs "static void ${REOPEN_METHOD##*.}(" "$PROJECT_DIR/Assets/_Project/Scripts/Editor"; then
    echo "Método $REOPEN_METHOD ainda não existe; reabrindo na última cena." >&2
    REOPEN_METHOD=""
  fi
  WINDOW_ID="$(wmctrl -lx | awk '$3 ~ /Unity-6000/ { print $1; exit }')"
  if [[ -n "$WINDOW_ID" ]]; then
    wmctrl -ic "$WINDOW_ID"
    for ((ATTEMPT = 0; ATTEMPT < 40; ATTEMPT++)); do
      if ! wmctrl -l | awk '{ print $1 }' | grep -qx "$WINDOW_ID"; then break; fi
      sleep 0.5
    done
    if wmctrl -l | awk '{ print $1 }' | grep -qx "$WINDOW_ID"; then
      echo "A Unity não fechou; confira se há um diálogo de alterações pendentes." >&2
      exit 1
    fi
  fi
  for ((ATTEMPT = 0; ATTEMPT < 40; ATTEMPT++)); do
    if ! fuser "$PROJECT_DIR/Temp/UnityLockfile" >/dev/null 2>&1; then break; fi
    sleep 0.5
  done
  if fuser "$PROJECT_DIR/Temp/UnityLockfile" >/dev/null 2>&1; then
    echo "A Unity ainda mantém o lock do projeto; nova instância não será iniciada." >&2
    exit 1
  fi
  if [[ -n "$REOPEN_METHOD" ]]; then
    exec "$PROJECT_DIR/Tools/unity.sh" -executeMethod "$REOPEN_METHOD"
  fi
  exec "$PROJECT_DIR/Tools/unity.sh"
fi

EDITOR_COMMAND=("$EDITOR_BIN")
if [[ "${1:-}" == --debug-startup ]]; then
  shift
  EDITOR_COMMAND=(gdb -nx -batch -iex "set debuginfod enabled off" \
    -ex "set pagination off" -ex run -ex "thread apply all bt 12" --args "$EDITOR_BIN")
fi

if [[ "${1:-}" == --verify || "${1:-}" == --verify-and-open ]]; then
  # Compila e roda todos os testes EditMode em batch; resultados em Logs/.
  mkdir -p "$PROJECT_DIR/Logs"
  "$PROJECT_DIR/Tools/unity.sh" -batchmode -runTests -testPlatform EditMode \
    -testResults "$PROJECT_DIR/Logs/TestResults.xml" -logFile "$PROJECT_DIR/Logs/Tests.log"
  if [[ "${1:-}" == --verify-and-open ]]; then
    nohup "$PROJECT_DIR/Tools/unity.sh" >"$PROJECT_DIR/Logs/Unity.stdout.log" 2>&1 </dev/null &
    echo "Unity iniciada com PID $!; logs em $PROJECT_DIR/Logs."
  fi
  exit 0
fi

mkdir -p "$LOCAL_DIR/config/unity3d" "$LOCAL_DIR/cache" "$LOCAL_DIR/data/unity3d" \
  "$LOCAL_DIR/state" "$LOCAL_DIR/tmp" "$LOCAL_DIR/upm" "$LOCAL_DIR/asset-store" "$PROJECT_DIR/Logs"

# HOME não é alterado. Os caminhos legados da Unity também apontam para cópias locais.
# Somente a licença existente é copiada; nenhuma credencial entra no versionamento.
if [[ ! -d "$LOCAL_DIR/config/unity3d/Unity/licenses" ]]; then
  for LICENSE_SOURCE in "${LICENSE_SOURCES[@]}"; do
    if [[ -d "$LICENSE_SOURCE/licenses" ]]; then
      mkdir -p "$LOCAL_DIR/config/unity3d/Unity"
      cp -a "$LICENSE_SOURCE/licenses" "$LOCAL_DIR/config/unity3d/Unity/"
      if [[ -d "$LICENSE_SOURCE/config" ]]; then
        cp -a "$LICENSE_SOURCE/config" "$LOCAL_DIR/config/unity3d/Unity/"
      fi
      break
    fi
  done
fi

# Pastas do HOME redirecionadas para .unity/. Como a raiz do sandbox é somente leitura,
# um destino inexistente não pode ser criado: nesse caso a pasta-mãe inteira é redirecionada.
HOME_BINDS=()
bind_home() {
  local source="$1" target="$2"
  mkdir -p "$source"
  if [[ -d "$target" ]]; then
    HOME_BINDS+=(--bind "$source" "$target")
  elif [[ -d "$(dirname "$target")" ]]; then
    HOME_BINDS+=(--bind "$(dirname "$source")" "$(dirname "$target")")
  else
    echo "Pasta $(dirname "$target") não existe; crie-a ou ajuste o Tools/unity.sh." >&2
    exit 1
  fi
}
bind_home "$LOCAL_DIR/cache" "$HOME/.cache"
bind_home "$LOCAL_DIR/config/unity3d" "$HOME/.config/unity3d"
# Destino padrão dos downloads da Asset Store no Linux (unity3d/Asset Store-5.x).
bind_home "$LOCAL_DIR/data/unity3d" "$HOME/.local/share/unity3d"

SOCKET_BINDS=()
HUB_ARGS=()
# Compartilha somente os sockets do Hub e da licença; os caches continuam locais.
# Considera tanto o Hub instalado em /mnt/SSD quanto a instalação padrão em /tmp.
HUB_USER="$(id -un)"
for SOCKET_NAME in Unity-hubIPCService.sock \
  "Unity-LicenseClient-$HUB_USER.sock" "Unity-LicenseClient-$HUB_USER-notifications.sock"; do
  for SOCKET_DIR in /mnt/SSD/Unity/.tmp "${TMPDIR:-/tmp}" /tmp; do
    SOCKET_SOURCE="$SOCKET_DIR/$SOCKET_NAME"
    if [[ -S "$SOCKET_SOURCE" && "$SOCKET_SOURCE" != "$LOCAL_DIR/tmp/$SOCKET_NAME" ]]; then
      SOCKET_BINDS+=(--ro-bind "$SOCKET_SOURCE" "/tmp/$SOCKET_NAME")
      if [[ "$SOCKET_NAME" == Unity-hubIPCService.sock ]]; then HUB_ARGS=(-useHub -hubIPC); fi
      break
    fi
  done
done
if [[ -d /tmp/.X11-unix ]]; then
  SOCKET_BINDS+=(--ro-bind /tmp/.X11-unix /tmp/.X11-unix)
fi
if [[ -n "${XDG_RUNTIME_DIR:-}" && -d "$XDG_RUNTIME_DIR" ]]; then
  SOCKET_BINDS+=(--ro-bind "$XDG_RUNTIME_DIR" "$XDG_RUNTIME_DIR")
fi

LOG_ARGS=(-logFile "$PROJECT_DIR/Logs/Editor.log")
for ARG in "$@"; do
  if [[ "$ARG" == -logFile ]]; then
    LOG_ARGS=()
    break
  fi
done

exec bwrap --new-session --ro-bind / / --dev-bind /dev /dev --proc /proc \
  --bind "$PROJECT_DIR" "$PROJECT_DIR" \
  --bind "$LOCAL_DIR/tmp" /tmp --bind "$LOCAL_DIR/tmp" /var/tmp \
  "${HOME_BINDS[@]}" \
  "${SOCKET_BINDS[@]}" \
  --setenv XDG_CONFIG_HOME "$LOCAL_DIR/config" \
  --setenv XDG_CACHE_HOME "$LOCAL_DIR/cache" \
  --setenv XDG_DATA_HOME "$LOCAL_DIR/data" \
  --setenv XDG_STATE_HOME "$LOCAL_DIR/state" \
  --setenv TMPDIR "$LOCAL_DIR/tmp" \
  --setenv UPM_CACHE_ROOT "$LOCAL_DIR/upm" \
  --setenv ASSETSTORE_CACHE_PATH "$LOCAL_DIR/asset-store" \
  --setenv DOTNET_CLI_HOME "$LOCAL_DIR/dotnet" \
  --setenv GDK_BACKEND x11 \
  --setenv GTK_IM_MODULE xim \
  --setenv SDL_VIDEODRIVER x11 \
  --setenv SDL_VIDEO_DRIVER x11 \
  --unsetenv WAYLAND_DISPLAY \
  --setenv XDG_SESSION_TYPE x11 \
  --chdir "$PROJECT_DIR" \
  "${EDITOR_COMMAND[@]}" -projectPath "$PROJECT_DIR" "${HUB_ARGS[@]}" "${LOG_ARGS[@]}" "$@"
