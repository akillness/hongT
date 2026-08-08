#!/usr/bin/env python3
"""Minimal client for the Blender MCP bridge (`user_default.mcp` extension).

Protocol (see the addon's `mcp_to_blender_server.py`): a NUL-terminated JSON
request `{"type": "execute", "code": "...", "strict_json": false}` and a
NUL-terminated JSON response `{"status": "ok"|"error", "stdout": ..., ...}`.

The repository contract (CLAUDE.md §3) runs Blender headless. Start a private
bridge that never touches an interactive session:

    /Applications/Blender.app/Contents/MacOS/Blender --background \
        --command blender_mcp --port 9877

then drive it:

    python3 tools/blender/mcp_client.py --port 9877 --file script.py
    python3 tools/blender/mcp_client.py --port 9877 --code 'print(bpy.app.version_string)'
"""

import argparse
import json
import socket
import sys

DEFAULT_HOST = "127.0.0.1"
DEFAULT_PORT = 9877
RECV_CHUNK = 65536


def execute(code, host=DEFAULT_HOST, port=DEFAULT_PORT, timeout=600.0):
    """Run `code` inside the bridged Blender and return the decoded response."""
    request = json.dumps({"type": "execute", "code": code, "strict_json": False})
    with socket.create_connection((host, port), timeout=timeout) as sock:
        sock.settimeout(timeout)
        sock.sendall(request.encode("utf-8") + b"\0")
        buf = bytearray()
        while b"\0" not in buf:
            chunk = sock.recv(RECV_CHUNK)
            if not chunk:
                raise RuntimeError("bridge closed the connection before responding")
            buf.extend(chunk)
    return json.loads(bytes(buf[: buf.index(b"\0")]).decode("utf-8"))


def main(argv=None):
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--host", default=DEFAULT_HOST)
    parser.add_argument("--port", type=int, default=DEFAULT_PORT)
    parser.add_argument("--timeout", type=float, default=600.0)
    source = parser.add_mutually_exclusive_group(required=True)
    source.add_argument("--code", help="python source to execute in Blender")
    source.add_argument("--file", help="path to a python file to execute in Blender")
    args = parser.parse_args(argv)

    code = args.code
    if code is None:
        with open(args.file, "r", encoding="utf-8") as handle:
            code = handle.read()

    response = execute(code, host=args.host, port=args.port, timeout=args.timeout)
    stdout = response.get("stdout", "")
    stderr = response.get("stderr", "")
    if stdout:
        sys.stdout.write(stdout if stdout.endswith("\n") else stdout + "\n")
    if stderr:
        sys.stderr.write(stderr if stderr.endswith("\n") else stderr + "\n")
    status = response.get("status")
    if status not in ("ok", "success"):
        sys.stderr.write("status={!r} message={}\n".format(status, response.get("message", "")))
        return 1
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
