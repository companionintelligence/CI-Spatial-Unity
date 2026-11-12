FROM nvidia/cuda:12.1.1-cudnn8-devel-ubuntu22.04

# GPU architectures the GroundingDINO and SAM 2 CUDA extensions are compiled for:
# Ampere (8.0 A100/A30, 8.6 RTX 30xx/A10/A40), Ada (8.9 RTX 40xx/L4/L40S) and
# Hopper (9.0 H100/H200), plus PTX so newer GPUs can JIT-compile the kernels.
# Narrow it to speed up a local build, e.g. --build-arg TORCH_CUDA_ARCH_LIST=8.9
ARG TORCH_CUDA_ARCH_LIST="8.0;8.6;8.9;9.0+PTX"

ENV DEBIAN_FRONTEND=noninteractive
ENV PYTHONUNBUFFERED=1
ENV CUDA_HOME=/usr/local/cuda
ENV PATH=/usr/local/cuda/bin:$PATH
ENV LD_LIBRARY_PATH=/usr/local/cuda/lib64:$LD_LIBRARY_PATH
ENV FORCE_CUDA=1
ENV TORCH_CUDA_ARCH_LIST="${TORCH_CUDA_ARCH_LIST}"

# Python 3.11 from Ubuntu 22.04's own archive (python3.11 in jammy-updates), in a
# virtualenv that comes first on PATH, so python3/pip/uvicorn below are 3.11.
# requirements.txt needs 3.11: pillow 13.0.0 declares Requires-Python >=3.11, and
# jammy's default python3 is 3.10. Every other pin, and torch 2.5.1+cu121 from the
# cu121 index, also resolves for 3.11.
RUN apt-get update && apt-get install -y \
    python3.11 python3.11-dev python3.11-venv git build-essential ninja-build cmake libgl1 libglib2.0-0 \
    && rm -rf /var/lib/apt/lists/*
RUN python3.11 -m venv /opt/venv
ENV VIRTUAL_ENV=/opt/venv
ENV PATH=/opt/venv/bin:$PATH

WORKDIR /app

RUN pip install torch torchvision torchaudio --index-url https://download.pytorch.org/whl/cu121

# flash-attn builds without isolation, so its build requirements must already be here
# (the system pip used to bring wheel along; a fresh virtualenv does not).
RUN pip install packaging wheel
RUN pip install --no-build-isolation flash-attn

COPY requirements.txt .
RUN pip install --no-cache-dir -r requirements.txt

RUN python3 - <<'PY'
import pathlib
import shutil
import site
import subprocess
import sys
import tempfile

repo_dir = pathlib.Path(tempfile.mkdtemp()) / "GroundingDINO"
subprocess.check_call([
    "git",
    "clone",
    "--depth",
    "1",
    "https://github.com/IDEA-Research/GroundingDINO.git",
    str(repo_dir),
])
subprocess.check_call([sys.executable, "setup.py", "build_ext", "--inplace"], cwd=repo_dir)

site_pkg = pathlib.Path(site.getsitepackages()[0]) / "groundingdino"
for so_file in (repo_dir / "groundingdino").glob("_C*.so"):
    shutil.copy2(so_file, site_pkg / so_file.name)
PY

# The model files are not in the build context (configs/ and checkpoints/ are
# gitignored). The entrypoint downloads them from pinned URLs and verifies their
# SHA-256 on first start; mount volumes on /app/configs and /app/checkpoints to keep
# them. Build with --build-arg FETCH_WEIGHTS_AT_BUILD=1 to bake them in instead.
COPY scripts/fetch_weights.py /app/scripts/fetch_weights.py
ARG FETCH_WEIGHTS_AT_BUILD=0
RUN mkdir -p /app/configs /app/checkpoints \
    && if [ "$FETCH_WEIGHTS_AT_BUILD" = "1" ]; then python3 /app/scripts/fetch_weights.py --root /app; fi

COPY app/ /app/app/

EXPOSE 8000 443

COPY entrypoint.sh /app/entrypoint.sh
# A Windows checkout can give the script CRLF endings, which break its #! line.
RUN sed -i 's/\r$//' /app/entrypoint.sh && chmod +x /app/entrypoint.sh

CMD ["/app/entrypoint.sh"]
