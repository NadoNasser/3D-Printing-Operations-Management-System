# PrintShop3D technical fixes

## Implemented

1. **Multiple print-file upload**
   - The Print Files page now accepts multiple `.stl`, `.3mf`, `.obj`, `.step`, and `.stp` files in one submission.
   - Maximum size: 100 MB per file and 300 MB total per submission.
   - Files are stored with server-generated names under `App_Data/PrintFiles/<OrderItemId>`.
   - The newest uploaded file is marked as the current version to preserve the existing `PRINT_FILES.IsCurrent` schema behavior.
   - The old `PageModel.File`-hiding `PrintFile File` property was removed, eliminating the CS0108 warning.

2. **Multipart request limit**
   - ASP.NET Core multipart body limit is set to 320 MB so a 300 MB upload batch can reach the application before application-level validation.

3. **Secure print-file download**
   - Download requests now require both `fileId` and `orderItemId`.
   - The server verifies that the file belongs to that order item before returning it.
   - Physical paths are constrained to the application `App_Data/PrintFiles` storage root.

4. **Order details now show print files**
   - Order Details lists every attached print file for each order item and provides a safe download link.
   - The existing Manage Files button remains available.

5. **Edit print files after order creation**
   - Added a dedicated Print File Edit page for files already attached to an order item.
   - From Order Details, each print file now has an Edit action.
   - From the Design Files page, each file also has an Edit action.
   - Editing supports changing the display name, extension, notes, current-file flag, replacing an uploaded file, or changing an external shared-drive/NAS path.
   - Replaced uploaded files are validated against the same supported extensions and 100 MB per-file limit.
   - Managed old physical files are removed only after the database update succeeds.

6. **EF Core decimal precision warnings**
   - Added explicit precision for previously unconfigured persisted decimal properties in purchase orders, purchase items, goods receipts, and the material stock view.

7. **Existing workflow fixes preserved**
   - Order trigger mapping remains configured with `HasTrigger("trg_Orders_LogStatusHistory")`.
   - Production `FAILED` status remains supported by the existing workflow rules.
   - Order status badge coloring remains centralized in `WorkflowRules.StatusBadgeClass`.

## Verification note

The source was statically checked after the changes. The current execution environment does not contain the .NET SDK (`dotnet` is not installed), so a real `dotnet build` could not be executed here. Run `dotnet build` on the Windows development machine before starting the application.

8. **Production Job Priority**
   - Production jobs support LOW / NORMAL / HIGH / URGENT priority.
   - The production queue displays and filters by priority.
   - Jobs with no meaningful update for 24 hours receive a one-level queue boost without overwriting the manual priority.
   - Priority and LastUpdatedAt are mapped in EF Core and the database migration is included as `db_008_production_job_priority.sql`.

9. **Order quotation uniqueness fix**
   - Included `db_009_order_quotation_unique_fix.sql` to replace the invalid normal UNIQUE constraint on nullable `ORDERS.QuotationID` with a filtered unique index.
   - This prevents the duplicate-NULL order creation error while still enforcing unique quotation linkage.

10. **Full multipart request limit**
   - Kestrel and ASP.NET Core form limits are both configured to 320 MB, allowing the intended 300 MB multi-file batch to reach application validation.

## Customer blacklist
Customers can be marked BLACKLISTED with an optional reason. Order creation shows a visible warning and asks for confirmation before continuing. Run db_010_customer_blacklist.sql once.
