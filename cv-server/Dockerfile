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

# Python 3.11 from the deadsnakes PPA, in a virtualenv that comes first on PATH, so
# python3/pip/uvicorn below are 3.11. requirements.txt needs 3.11: pillow 13.0.0 declares
# Requires-Python >=3.11, and jammy's default python3 is 3.10. Not jammy's own python3.11
# package: that is 3.11.0~rc1, a 2022 pre-release that gets no fixes, and on it importing
# timm 0.9.16 with torch 2.5.1 kills the interpreter (SIGSEGV in torch.jit.script). timm is
# imported by GroundingDINO and by Florence-2's model code. The deadsnakes build (3.11.18
# when this was written) runs the same packages; the CUDA base image stays as it was.
RUN apt-get update && apt-get install -y software-properties-common \
    && add-apt-repository -y ppa:deadsnakes/ppa \
    && apt-get install -y \
    python3.11 python3.11-dev python3.11-venv git build-essential ninja-build cmake libgl1 libglib2.0-0 \
    && rm -rf /var/lib/apt/lists/*
RUN python3.11 -m venv /opt/venv
ENV VIRTUAL_ENV=/opt/venv
ENV PATH=/opt/venv/bin:$PATH

WORKDIR /app

# torch 2.5.1 is the last release with CUDA 12.1 wheels. Pinned, like everything below,
# to what the release-candidate image was built and checked with.
RUN pip install torch==2.5.1 torchvision==0.20.1 torchaudio==2.5.1 --index-url https://download.pytorch.org/whl/cu121

# flash-attn builds without isolation, so its build requirements must already be here
# (the system pip used to bring wheel along; a fresh virtualenv does not). 2.8.3.post2
# downloads a prebuilt wheel for CUDA 12, torch 2.5 and cp311, so nothing is compiled.
RUN pip install packaging==26.3 wheel==0.48.0
RUN pip install --no-build-isolation flash-attn==2.8.3.post2

# --no-build-isolation: GroundingDINO and SAM 2 compile CUDA extensions, which must be built
# against the torch 2.5.1+cu121 above, for this image's CUDA 12.1 toolkit. With isolation,
# pip builds SAM 2 against the newest torch on PyPI instead (2.15.0, for CUDA 13), which also
# meant a multi-gigabyte download per build, and its extension was skipped. GroundingDINO's
# setup.py even pip-installs torch itself when it cannot import it.
# GroundingDINO's _C extension now comes from this build, so the separate in-place rebuild
# of an unpinned clone that used to follow is gone.
COPY requirements.txt .
RUN pip install --no-cache-dir --no-build-isolation -r requirements.txt

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
