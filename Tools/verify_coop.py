#!/usr/bin/env python3
"""Ensaio Editor Host + build Linux Client; saídas confinadas ao projeto."""
import argparse
import json
from pathlib import Path
import subprocess
import sys
import time


ROOT = Path(__file__).resolve().parents[1]
LAUNCHER = ROOT / "Tools/unity.sh"
LOGS = ROOT / "Logs/Network"


def evidence(path):
    try:
        return json.loads(path.read_text())
    except (FileNotFoundError, json.JSONDecodeError):
        return {}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("protocol", choices=("udp", "ws"))
    parser.add_argument("--port", default="7777")
    args = parser.parse_args()
    LOGS.mkdir(parents=True, exist_ok=True)
    subprocess.run([str(LAUNCHER), "--close-editor"], cwd=ROOT, check=True)
    outputs = {role: LOGS / f"{args.protocol}-{role}.json" for role in ("host", "client")}
    for path in outputs.values():
        path.unlink(missing_ok=True)

    common = ["--coop-protocol", args.protocol, "--coop-port", args.port, "--coop-smoke"]
    host_log = (LOGS / f"{args.protocol}-host.stdout.log").open("w")
    subprocess.Popen([
        str(LAUNCHER), "-executeMethod", "GameYunit.KitBridge.Editor.CoopProjectSetup.PlayCoop",
        "-logFile", str(LOGS / f"{args.protocol}-host.log"),
        "--coop-role", "host", *common,
    ], cwd=ROOT, stdout=host_log, stderr=subprocess.STDOUT, start_new_session=True)
    print(f"{args.protocol}: aguardando o Editor Host…", flush=True)
    deadline = time.monotonic() + 180
    while not evidence(outputs["host"]) and time.monotonic() < deadline:
        time.sleep(1)
    if not evidence(outputs["host"]):
        print("O Host não iniciou o ensaio. Consulte o log.", flush=True)
        return 1

    client_log = (LOGS / f"{args.protocol}-client.stdout.log").open("w")
    client = subprocess.Popen([
        str(LAUNCHER), "--client", "-screen-fullscreen", "0", "-screen-width", "960", "-screen-height", "540",
        "-logFile", str(LOGS / f"{args.protocol}-client.log"),
        "--coop-role", "client", *common,
    ], cwd=ROOT, stdout=client_log, stderr=subprocess.STDOUT, start_new_session=True)
    deadline = time.monotonic() + 190
    previous = {}
    while time.monotonic() < deadline:
        current = {role: evidence(path) for role, path in outputs.items()}
        stages = {role: value.get("stage", "starting") for role, value in current.items()}
        if stages != previous:
            print(json.dumps(stages, ensure_ascii=False), flush=True)
            previous = stages
        if all(stage in ("passed", "failed") for stage in stages.values()):
            print(json.dumps(current, ensure_ascii=False, indent=2), flush=True)
            try:
                client.wait(timeout=15)
            except subprocess.TimeoutExpired:
                print("O cliente ainda está encerrando; consulte seu log.", flush=True)
            return 0 if all(value.get("success") for value in current.values()) else 1
        if client.poll() is not None and not current["client"]:
            print(f"Cliente encerrou antes de produzir evidência: {client.returncode}", flush=True)
            return 1
        time.sleep(1)
    print("Ensaio excedeu o prazo. Os processos e logs foram preservados para diagnóstico.", flush=True)
    return 1


if __name__ == "__main__":
    sys.exit(main())
