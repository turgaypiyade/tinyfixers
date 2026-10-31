"""Current revision verifier. Rejected v1 verifier remains in Revisions/v1/."""
from pathlib import Path
import runpy
runpy.run_path(str(Path(__file__).with_name("verify_v2.py")), run_name="__main__")
