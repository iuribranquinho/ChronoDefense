"""Prova de aceleração: identifica o backend (HIP / DirectML / CPU) e mede um matmul."""
import time

import torch

print("torch", torch.__version__, "| hip:", getattr(torch.version, "hip", None))
backend, dev = "cpu", torch.device("cpu")
if torch.cuda.is_available():
    backend, dev = "hip" if torch.version.hip else "cuda", torch.device("cuda:0")
    print("device:", torch.cuda.get_device_name(0), torch.cuda.get_device_properties(0).gcnArchName
          if hasattr(torch.cuda.get_device_properties(0), "gcnArchName") else "")
else:
    try:
        import torch_directml
        backend, dev = "directml", torch_directml.device()
        print("device:", torch_directml.device_name(torch_directml.default_device()))
    except ImportError:
        pass

n = 2048
a = torch.randn(n, n, device=dev)
b = torch.randn(n, n, device=dev)
(a @ b).sum().item()  # aquecimento
t = time.perf_counter()
for _ in range(20):
    c = a @ b
s = c.sum().item()  # força sincronização
dt = time.perf_counter() - t
cpu_a, cpu_b = a.cpu(), b.cpu()
t = time.perf_counter()
for _ in range(20):
    cpu_c = cpu_a @ cpu_b
dtc = time.perf_counter() - t
print(f"backend={backend} tensor.device={c.device} | 20x matmul {n}: {dt*1000:.1f} ms "
      f"({20*2*n**3/dt/1e12:.2f} TFLOPS) | CPU: {dtc*1000:.1f} ms | max|diff|={(c.cpu()-cpu_c).abs().max().item():.3e}")
