#!/usr/bin/env bash
# Runs ONE Unity batchmode DevHarness entry point and gates on its report.
#
#   tools/run-batchmode-check.sh <fully.qualified.Method> <report.txt>
#
# Unity is launched detached with setsid + nohup (a plain `nohup &`
# dies with the shell, and a foreground run is killed by the caller's timeout),
# the run is judged on the report file the method writes plus the compile log:
#
#   * any 'error CS' in the Unity log  -> FAIL (a compile break);
#   * no report file appears           -> FAIL (the method never ran / crashed);
#   * report without the PASS SUMMARY  -> FAIL (a check failed);
#   * otherwise                        -> PASS, report printed.
#
# Exit code is non-zero on any FAIL, so this can gate a whole check sequence.
#
# NOTE: this launches with -nographics, which is right for the compute-only
# harnesses (pick/order/driver checks are pure math) but renders NOTHING - a
# camera.Render() under it comes back as one flat clear-colour frame. A harness
# that captures (S2MActorClickRun) must be launched manually with -batchmode
# only, keeping DISPLAY :0.
#
# Environment:
#   UNITY_BIN        Unity executable (default: Unity on PATH; falls back to the
#                    newest Editor under ~/Unity/Hub/Editor when PATH has none).
#   TIMEOUT_SECONDS  how long to wait for the report (default: 240).
#   SH2_REPORT       alternative way to pass the report path (argument wins).
set -u

METHOD="${1:-Assets.Code.Stronghold2.DevHarness.S2MActorControlRun.Main}"
REPORT="${2:-${SH2_REPORT:-/tmp/sh2_actor_control_report.txt}}"
LOG="${UNITY_LOG:-/tmp/sh2-batchmode-check.log}"
TIMEOUT_SECONDS="${TIMEOUT_SECONDS:-240}"

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
cd "$SCRIPT_DIR/.." || exit 2

if [ -z "${UNITY_BIN:-}" ]; then
  if command -v Unity >/dev/null 2>&1; then
    UNITY_BIN="Unity"
  else
    # No Unity on PATH in this environment - take the newest installed Editor
    # from the Hub so the check still runs.
    UNITY_BIN="$(ls -d "$HOME"/Unity/Hub/Editor/*/Editor/Unity 2>/dev/null | sort | tail -1)"
  fi
fi
if [ -z "$UNITY_BIN" ] || [ ! -x "$UNITY_BIN" ]; then
  echo "run-batchmode-check: no Unity executable found (set UNITY_BIN)" >&2
  exit 2
fi

rm -f "$REPORT"
: > "$LOG"

# The method writes its report at the path the caller asked for.
export SH2_ACTOR_CONTROL_REPORT="$REPORT"

echo "run-batchmode-check: method=$METHOD report=$REPORT log=$LOG unity=$UNITY_BIN"
setsid nohup "$UNITY_BIN" -batchmode -nographics -projectPath "$PWD" \
  -logFile "$LOG" -executeMethod "$METHOD" -quit \
  > /dev/null 2>&1 < /dev/null &
disown

# Poll for the report - the method writes it as its last act, so its presence
# with a SUMMARY line means the run reached the end.
DEADLINE=$(( $(date +%s) + TIMEOUT_SECONDS ))
while [ "$(date +%s)" -lt "$DEADLINE" ]; do
  if [ -f "$REPORT" ] && grep -q "SUMMARY:" "$REPORT"; then
    break
  fi
  sleep 2
done

if grep -q "error CS" "$LOG"; then
  echo "run-batchmode-check: FAIL - compile error(s) in $LOG"
  grep -n "error CS" "$LOG" | head -20
  exit 1
fi

if [ ! -f "$REPORT" ]; then
  echo "run-batchmode-check: FAIL - no report at $REPORT after ${TIMEOUT_SECONDS}s"
  tail -30 "$LOG"
  exit 1
fi

cat "$REPORT"

if grep -q "SUMMARY: PASS with 0 failure(s)" "$REPORT"; then
  echo "run-batchmode-check: PASS ($(basename "$REPORT"))"
  exit 0
fi

echo "run-batchmode-check: FAIL - $(basename "$REPORT") has a failing SUMMARY"
exit 1
