-- Script to update existing manufacturer documents to be linked to equipment models instead of individual equipment
-- This ensures that documents uploaded for one piece of equipment are available for all equipment of the same model

-- Update existing ManufacturerDocuments to link to EquipmentModel instead of individual Equipment
UPDATE MD
SET MD.EquipmentModelId = E.EquipmentModelId,
    MD.UploadedByEquipmentId = MD.EquipmentModelId  -- Store original equipment ID in new field
FROM ManufacturerDocuments MD
INNER JOIN Equipment E ON MD.EquipmentModelId = E.EquipmentId  -- Note: EquipmentModelId currently contains EquipmentId due to migration
WHERE E.EquipmentModelId IS NOT NULL;

-- Update MaintenanceRecommendations to link to EquipmentModel
UPDATE MR
SET MR.EquipmentModelId = E.EquipmentModelId
FROM MaintenanceRecommendations MR
INNER JOIN Equipment E ON MR.EquipmentId = E.EquipmentId
WHERE E.EquipmentModelId IS NOT NULL;

-- Remove duplicate documents for the same equipment model (keep the latest one)
WITH DuplicateDocuments AS (
    SELECT 
        DocumentId,
        ROW_NUMBER() OVER (
            PARTITION BY EquipmentModelId, FileName 
            ORDER BY UploadDate DESC
        ) as rn
    FROM ManufacturerDocuments
    WHERE EquipmentModelId IS NOT NULL
)
DELETE FROM ManufacturerDocuments 
WHERE DocumentId IN (
    SELECT DocumentId 
    FROM DuplicateDocuments 
    WHERE rn > 1
);

-- Verify the updates
SELECT 
    COUNT(*) as TotalDocuments,
    COUNT(DISTINCT EquipmentModelId) as UniqueModelsWithDocs
FROM ManufacturerDocuments
WHERE EquipmentModelId IS NOT NULL;

SELECT 
    EM.ModelName,
    COUNT(MD.DocumentId) as DocumentCount
FROM EquipmentModels EM
LEFT JOIN ManufacturerDocuments MD ON EM.EquipmentModelId = MD.EquipmentModelId
GROUP BY EM.EquipmentModelId, EM.ModelName
HAVING COUNT(MD.DocumentId) > 0
ORDER BY DocumentCount DESC;
