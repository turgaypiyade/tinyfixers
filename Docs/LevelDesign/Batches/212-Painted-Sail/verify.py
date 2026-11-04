"""Run the active user-requested 212 revision audit; v1 verifier is archived in Revisions/."""
from pathlib import Path
import runpy
runpy.run_path(str(Path(__file__).with_name("verify_bottom.py")),run_name="__main__")
