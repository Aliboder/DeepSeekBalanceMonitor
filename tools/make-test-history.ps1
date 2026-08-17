# 开发测试工具：生成"消费突增"模拟历史数据（勿在产品环境使用）
$path = "D:\SystemFiles\Documents\QuotaMonitor\余额记录.json"

$pairs = @(
    @('2026-07-30 18:00:00', 130),
    @('2026-07-31 18:00:00', 125),
    @('2026-08-01 18:00:00', 120),
    @('2026-08-02 18:00:00', 115),
    @('2026-08-03 18:00:00', 110),
    @('2026-08-04 18:00:00', 100)
)

$items = foreach ($p in $pairs) {
    $dt = [datetime]::ParseExact($p[0], 'yyyy-MM-dd HH:mm:ss', $null)
    $ms = [DateTimeOffset]$dt | ForEach-Object { $_.ToUnixTimeMilliseconds() }
    '{"time":"/Date(' + $ms + ')/","balance":' + $p[1] + '}'
}

$json = '{"records":[' + ($items -join ',') + ']}'
Set-Content -Path $path -Value $json -Encoding UTF8
Write-Host "test history written to $path"
