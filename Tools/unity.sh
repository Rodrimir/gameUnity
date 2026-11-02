#!/usr/bin/env bash
set -euo pipefail

PROJECT_DIR="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")/.." && pwd -P)"
EDITOR_BIN="${GAMEYUNIT_UNITY_EDITOR:-/mnt/SSD/Unity/editors/6000.3.27f2/Editor/Unity}"
LOCAL_DIR="$PROJECT_DIR/.unity"

if [[ "${1:-}" == --status ]]; then
  ps -eo pid,etime,stat,comm | awk 'NR == 1 || tolower($4) ~ /unity|bwrap/'
  wmctrl -lpGx | awk '$8 ~ /[Uu]nity/ { print $1, "pid=" $3, $8, substr($0, index($0, $10)) }'
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

if [[ "${1:-}" == --capture-window ]]; then
  WINDOW_ID="$(wmctrl -lx | awk '$3 ~ /Unity-6000/ { print $1; exit }')"
  if [[ -z "$WINDOW_ID" ]]; then
    echo "Janela da Unity não encontrada." >&2
    exit 1
  fi
  mkdir -p "$PROJECT_DIR/Logs"
  mkdir -p "$LOCAL_DIR/tmp"
  export MAGICK_TEMPORARY_PATH="$LOCAL_DIR/tmp"
  exec import -window "$WINDOW_ID" "$PROJECT_DIR/Logs/UnityWindow.png"
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

if [[ "${1:-}" == --reopen-preview || "${1:-}" == --reopen-game || "${1:-}" == --reopen-play ]]; then
  case "${1:-}" in
    --reopen-game) REOPEN_METHOD=GameYunit.Editor.ProjectSetup.OpenGame ;;
    --reopen-play) REOPEN_METHOD=GameYunit.Editor.ProjectSetup.PlayGameForMenuReview ;;
    *) REOPEN_METHOD=GameYunit.Editor.ProjectSetup.OpenAndCaptureCharacters ;;
  esac
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
  exec "$PROJECT_DIR/Tools/unity.sh" -executeMethod "$REOPEN_METHOD"
fi

EDITOR_COMMAND=("$EDITOR_BIN")
if [[ "${1:-}" == --debug-startup ]]; then
  shift
  EDITOR_COMMAND=(gdb -nx -batch -iex "set debuginfod enabled off" \
    -ex "set pagination off" -ex run -ex "thread apply all bt 12" --args "$EDITOR_BIN")
fi

if [[ "${1:-}" == --verify-and-open ]]; then
  mkdir -p "$PROJECT_DIR/Logs"
  "$PROJECT_DIR/Tools/unity.sh" -batchmode -runTests \
    -testPlatform EditMode -assemblyNames GameYunit.Editor.Tests \
    -testResults "$PROJECT_DIR/Logs/TestResults.xml" -logFile "$PROJECT_DIR/Logs/Tests.log"
  nohup "$PROJECT_DIR/Tools/unity.sh" -executeMethod GameYunit.Editor.ProjectSetup.OpenAndCaptureCharacters \
    >"$PROJECT_DIR/Logs/Unity.stdout.log" 2>&1 </dev/null &
  echo "Unity iniciada com PID $!; logs em $PROJECT_DIR/Logs."
  exit 0
fi

mkdir -p "$LOCAL_DIR/config/unity3d" "$LOCAL_DIR/cache" "$LOCAL_DIR/data" \
  "$LOCAL_DIR/state" "$LOCAL_DIR/tmp" "$LOCAL_DIR/upm" "$PROJECT_DIR/Logs"

# HOME não é alterado. Os caminhos legados da Unity também apontam para cópias locais.
# Somente a licença existente é copiada; nenhuma credencial entra no versionamento.
LICENSE_SOURCE=/mnt/SSD/Unity/xdg/config/unity3d/Unity
if [[ ! -d "$LOCAL_DIR/config/unity3d/Unity/licenses" && -d "$LICENSE_SOURCE/licenses" ]]; then
  mkdir -p "$LOCAL_DIR/config/unity3d/Unity"
  cp -a "$LICENSE_SOURCE/licenses" "$LOCAL_DIR/config/unity3d/Unity/"
  if [[ -d "$LICENSE_SOURCE/config" ]]; then
    cp -a "$LICENSE_SOURCE/config" "$LOCAL_DIR/config/unity3d/Unity/"
  fi
fi

SOCKET_BINDS=()
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
  --bind "$LOCAL_DIR/cache" "$HOME/.cache" \
  --bind "$LOCAL_DIR/config/unity3d" "$HOME/.config/unity3d" \
  "${SOCKET_BINDS[@]}" \
  --setenv XDG_CONFIG_HOME "$LOCAL_DIR/config" \
  --setenv XDG_CACHE_HOME "$LOCAL_DIR/cache" \
  --setenv XDG_DATA_HOME "$LOCAL_DIR/data" \
  --setenv XDG_STATE_HOME "$LOCAL_DIR/state" \
  --setenv TMPDIR "$LOCAL_DIR/tmp" \
  --setenv UPM_CACHE_ROOT "$LOCAL_DIR/upm" \
  --setenv UNITY_ASSET_STORE_CACHE_PATH "$LOCAL_DIR/asset-store" \
  --setenv DOTNET_CLI_HOME "$LOCAL_DIR/dotnet" \
  --setenv GDK_BACKEND x11 \
  --setenv GTK_IM_MODULE xim \
  --setenv SDL_VIDEODRIVER x11 \
  --setenv SDL_VIDEO_DRIVER x11 \
  --unsetenv WAYLAND_DISPLAY \
  --setenv XDG_SESSION_TYPE x11 \
  --chdir "$PROJECT_DIR" \
  "${EDITOR_COMMAND[@]}" -projectPath "$PROJECT_DIR" "${LOG_ARGS[@]}" "$@"
