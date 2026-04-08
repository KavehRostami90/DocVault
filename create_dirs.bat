@echo off
mkdir "D:\Projects\DocVault\src\DocVault.Api\Contracts\Admin"
mkdir "D:\Projects\DocVault\src\DocVault.Application\UseCases\Admin\GetAdminStats"
mkdir "D:\Projects\DocVault\src\DocVault.Application\UseCases\Admin\ReindexDocument"

echo Verifying directories created:
if exist "D:\Projects\DocVault\src\DocVault.Api\Contracts\Admin" echo ✓ Admin contracts directory exists
if exist "D:\Projects\DocVault\src\DocVault.Application\UseCases\Admin\GetAdminStats" echo ✓ GetAdminStats directory exists
if exist "D:\Projects\DocVault\src\DocVault.Application\UseCases\Admin\ReindexDocument" echo ✓ ReindexDocument directory exists
