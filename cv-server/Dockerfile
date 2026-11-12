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

RUN apt-get update && apt-get install -y \
    python3.10 python3-pip git build-essential ninja-build cmake libgl1 libglib2.0-0 \
    && rm -rf /var/lib/apt/lists/*

WORKDIR /app

RUN pip3 install torch torchvision torchaudio --index-url https://download.pytorch.org/whl/cu121

RUN pip3 install packaging
RUN pip3 install --no-build-isolation flash-attn

COPY requirements.txt .
RUN pip3 install --no-cache-dir -r requirements.txt

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

COPY app/ /app/app/
COPY configs/ /app/configs/
COPY checkpoints/ /app/checkpoints/

EXPOSE 8000 443

COPY entrypoint.sh /app/entrypoint.sh
RUN chmod +x /app/entrypoint.sh

CMD ["/app/entrypoint.sh"]
