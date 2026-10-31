These excerpts preserve the production VFX behavior before the 2026-10-30 performance changes. They are test inputs, not runtime scripts. Cached procedural sprites are supplied by rendering doubles.

`csi Tools/check_vfx_hotpaths.csx` compares the current production methods with these fixed excerpts, including their motion/fade curves and managed allocation counts. It does not require an old Git HEAD, Unity Play Mode or a project build. The allocation results exclude Unity's native renderer, canvas rebuilding and device GPU costs.
