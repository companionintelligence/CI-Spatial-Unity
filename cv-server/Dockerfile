FROM nvidia/cuda:12.1.1-cudnn8-runtime-ubuntu22.04

ENV DEBIAN_FRONTEND=noninteractive
ENV PYTHONUNBUFFERED=1
ENV CUDA_HOME=/usr/local/cuda

RUN apt-get update && apt-get install -y \
    python3.10 python3-pip git build-essential libgl1 \
    && rm -rf /var/lib/apt/lists/*

WORKDIR /app

RUN pip3 install torch torchvision torchaudio --index-url https://download.pytorch.org/whl/cu121

COPY requirements.txt .
RUN pip3 install --no-cache-dir -r requirements.txt

COPY app/ /app/app/
COPY configs/ /app/configs/
COPY checkpoints/ /app/checkpoints/

EXPOSE 8000

CMD ["uvicorn", "app.main:app", "--host", "0.0.0.0", "--port", "8000"]
