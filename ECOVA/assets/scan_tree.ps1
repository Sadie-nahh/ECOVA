Get-ChildItem -Path "e:\ĐỒ ÁN CUỐI KÌ CNPM\ECOVA" -Recurse -File | Where-Object { $_.Extension -in ".cs", ".json", ".xml", ".sql", ".md" } | Select-Object FullName | ForEach-Object {
    $relativePath = $_.FullName.Substring("e:\ĐỒ ÁN CUỐI KÌ CNPM\ECOVA".Length + 1)
    Write-Output $relativePath
} | Out-File -FilePath "e:\ĐỒ ÁN CUỐI KÌ CNPM\ECOVA\tree.txt" -Encoding UTF8
