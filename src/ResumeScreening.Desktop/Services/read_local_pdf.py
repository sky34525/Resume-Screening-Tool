from pathlib import Path
import sys


def main() -> int:
    if len(sys.argv) != 4:
        print("invalid arguments", file=sys.stderr)
        return 2

    source = Path(sys.argv[1])
    destination = Path(sys.argv[2])
    max_size = int(sys.argv[3])
    data = source.read_bytes()

    if len(data) > max_size:
        print("file too large", file=sys.stderr)
        return 4

    if not data.startswith(b"%PDF-"):
        print("not a PDF", file=sys.stderr)
        return 3

    destination.write_bytes(data)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
