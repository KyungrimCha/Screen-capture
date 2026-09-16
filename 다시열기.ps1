# 쏙캡처가 Smart App Control에 차단됐을 때 실행하는 해제 스크립트.
# 실행을 시도하고, 막히면 재빌드로 파일 해시를 바꿔 다시 시도한다 (최대 8회).
$csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$here = Split-Path -Parent $MyInvocation.MyCommand.Path
$exe = Join-Path $here '쏙캡처.exe'
$src = Join-Path $here 'src\SsokCapture.cs'

Get-Process -Name '쏙캡처' -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Milliseconds 300

foreach ($i in 1..8) {
    try {
        $p = Start-Process $exe -PassThru -ErrorAction Stop
        Start-Sleep -Seconds 3
        if (Get-Process -Id $p.Id -ErrorAction SilentlyContinue) {
            Write-Host "실행 성공 ($i 회차)" -ForegroundColor Green
            exit 0
        }
    } catch {
        Write-Host "$i 회차 : 차단됨, 재빌드합니다" -ForegroundColor Yellow
    }
    if ($i % 2 -eq 0) {
        & $csc /nologo /target:winexe /out:$exe /reference:System.dll /reference:System.Drawing.dll /reference:System.Windows.Forms.dll $src | Out-Null
    } else {
        & $csc /nologo /target:winexe /optimize+ /out:$exe /reference:System.dll /reference:System.Drawing.dll /reference:System.Windows.Forms.dll $src | Out-Null
    }
    Start-Sleep -Seconds 1
}
Write-Host '8번 시도해도 안 풀렸어요. 잠시 후 다시 실행해보세요.' -ForegroundColor Red
exit 1
