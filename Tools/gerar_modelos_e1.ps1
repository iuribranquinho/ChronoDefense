# Gera os modelos .glb do MVP (Era 1 / Tundra) e normaliza pivô, escala e textura.
# Uso (com o Unity Editor FECHADO):  powershell -ExecutionPolicy Bypass -File Tools\gerar_modelos_e1.ps1
#   -SomenteNormalizar : não gera de novo; só reorienta/reescala/retexturiza os .glb que já existem (segundos)
# Imagens de referência em Tools\refs\E1\ (PNG, personagem sozinho, fundo transparente).
# Imagens que faltarem são puladas com aviso; o resto é gerado.
param([switch]$SomenteNormalizar)
$ErrorActionPreference = "Stop"
$tools = $PSScriptRoot
$refs  = Join-Path $tools "refs\E1"
$art   = Join-Path (Split-Path $tools -Parent) "Assets\_ChronoDefense\Art"
$logs  = Join-Path $tools "output\E1"
$py    = Join-Path $tools "TripoSR\venv\Scripts\python.exe"   # tem trimesh e Pillow, usado na normalização

# Hunyuan gera só a forma (sem cor): a imagem é projetada como textura no normalize_glb.py.
# TripoSR usa --bake-texture: textura UV, porque o URP/Lit ignora cor de vértice.
$jobs = @(
# Front = para onde o personagem olha NA IMAGEM: image (de frente), left/right (perfil, cabeça à esquerda/direita).
# Os creeps assumem perfil com a cabeça à esquerda; ajuste quando as imagens existirem.
  @{ Ref="HER_01_Krog.png";           Out="Heroes\HER_01_Krog\HER_01_Krog.glb";  Engine="hunyuan"; Height=1.8;  Front="image" },
  @{ Ref="BOS_010_KrogCorrupted.png"; Out="Bosses\BOS_010_KrogCorrupted.glb";    Engine="hunyuan"; Height=4.0;  Front="image" },
  @{ Ref="CRP_E1_GreyWolf.png";       Out="Creeps\E1\CRP_E1_GreyWolf.glb";       Engine="triposr"; Height=1.0;  Front="left"  },
  @{ Ref="CRP_E1_SabreTiger.png";     Out="Creeps\E1\CRP_E1_SabreTiger.glb";     Engine="triposr"; Height=1.0;  Front="left"  },
  @{ Ref="CRP_E1_WoollyRhino.png";    Out="Creeps\E1\CRP_E1_WoollyRhino.glb";    Engine="triposr"; Height=1.0;  Front="left"  }
)

if (-not $SomenteNormalizar -and (Get-Process -Name "Unity" -ErrorAction SilentlyContinue)) {
  throw "Feche o Unity Editor antes: com 4 GB de VRAM a geração fica ~10x mais lenta (RELATORIO_Geracao3D §5)."
}

$todo = @()
foreach ($j in $jobs) {
  $img = Join-Path $refs $j.Ref
  if (Test-Path $img) { $j.Image = $img; $todo += $j }
  else { Write-Warning "Sem imagem de referência: $img (pulando $($j.Out))" }
}
if ($todo.Count -eq 0) { throw "Nenhuma imagem em $refs" }

New-Item -ItemType Directory -Force -Path $logs | Out-Null
$results = @()
foreach ($j in $todo) {
  $out = Join-Path $art $j.Out
  $report = Join-Path $logs ([IO.Path]::GetFileNameWithoutExtension($out) + ".json")
  Write-Host "`n=== $($j.Out)  [$($j.Engine)]  <- $($j.Image)" -ForegroundColor Cyan
  if ($SomenteNormalizar) {
    if (-not (Test-Path $out)) { Write-Warning "Sem .glb para normalizar: $out"; continue }
  } else {
    $extra = @()
    if ($j.Engine -eq "triposr") { $extra = @("--bake-texture") }
    py (Join-Path $tools "generate_3d.py") --engine $j.Engine $j.Image -o $out --report $report @extra
    if ($LASTEXITCODE -ne 0) { throw "generate_3d.py falhou em $($j.Out)" }
  }
  # Orientação achada comparando a silhueta com a imagem; Hunyuan (sem cor) recebe a imagem projetada como textura.
  $norm = @((Join-Path $tools "normalize_glb.py"), $out, "--image", $j.Image, "--height", $j.Height, "--front", $j.Front)
  if ($j.Engine -eq "hunyuan") { $norm += "--project" }
  $results += (& $py @norm)
  if ($LASTEXITCODE -ne 0) { throw "normalize_glb.py falhou em $($j.Out)" }
}
Write-Host "`nResultado (altura em m, base Y, triângulos, ângulo achado, IoU da silhueta):" -ForegroundColor Green
$results | ForEach-Object { Write-Host $_ }
Write-Host "Relatórios de geração em $logs"
Write-Host "Próximo passo: abrir o Unity e rodar Tools > Chrono > Importar modelos 3D (Era 1)." -ForegroundColor Green
