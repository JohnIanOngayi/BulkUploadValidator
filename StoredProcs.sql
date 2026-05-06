-- --------------------------------------------------------
-- LINK REPOSITORY SPs
-- --------------------------------------------------------

DROP PROCEDURE IF EXISTS sp_Bulk_GetAllValidLinkTypes;
DELIMITER //
CREATE PROCEDURE sp_Bulk_GetAllValidLinkTypes()
BEGIN
    SELECT
        LinkTypeID  AS LinkTypeId,
        LinkTypeName,
        IsDelete    AS IsDeleted,
        IsActive
    FROM
        LinkTypeMaster
    WHERE
        IsDelete = 0 AND IsActive = 1
    ORDER BY
        LinkTypeName ASC;
END//
DELIMITER ;

-- --------------------------------------------------------

DROP PROCEDURE IF EXISTS sp_Bulk_GetAllValidSubCounties;
DELIMITER //
CREATE PROCEDURE sp_Bulk_GetAllValidSubCounties()
BEGIN
    SELECT
        s.SubCountyID   AS SubCountyId,
        s.SubCountyName,
        c.CountyID      AS CountyId,
        c.CountyName
    FROM
        SubCountyMaster s
    JOIN
        County_Master c ON s.CountyID = c.CountyID
    ORDER BY
        s.SubCountyName ASC;
END//
DELIMITER ;

-- --------------------------------------------------------

DROP PROCEDURE IF EXISTS sp_Bulk_GetExistentLinks;
DELIMITER //
CREATE PROCEDURE sp_Bulk_GetExistentLinks()
BEGIN
    SELECT LinkName FROM LinkMaster;
END//
DELIMITER ;

-- --------------------------------------------------------
-- SITE REPOSITORY SPs
-- --------------------------------------------------------

DROP PROCEDURE IF EXISTS sp_Bulk_GetAllValidSiteTypes;
DELIMITER //
CREATE PROCEDURE sp_Bulk_GetAllValidSiteTypes()
BEGIN
    SELECT
        SiteTypeID  AS SiteTypeId,
        SiteTypeName,
        IsDelete    AS IsDeleted,
        IsActive
    FROM
        SiteTypeMaster
    WHERE
        IsDelete = 0 AND IsActive = 1
    ORDER BY
        SiteTypeName ASC;
END//
DELIMITER ;

-- --------------------------------------------------------

DROP PROCEDURE IF EXISTS sp_Bulk_GetAllValidWards;
DELIMITER //
CREATE PROCEDURE sp_Bulk_GetAllValidWards()
BEGIN
    SELECT
        w.WardID            AS WardId,
        w.WardName,
        c.CountyId,
        c.CountyName,
        sc.SubCountyID      AS SubCountyId,
        sc.SubCountyName,
        cs.ConstituencyId,
        cs.ConstituencyName
    FROM WardMaster w
    JOIN ConstituencyMaster cs
        ON cs.ConstituencyID = w.ConstituencyId
    JOIN SubCountyMaster sc
        ON sc.SubCountyID = cs.SubCountyID
    JOIN County_Master c
        ON sc.CountyID = c.CountyId
    ORDER BY WardName ASC;
END//
DELIMITER ;

-- --------------------------------------------------------

DROP PROCEDURE IF EXISTS sp_Bulk_GetExistentSites;
DELIMITER //
CREATE PROCEDURE sp_Bulk_GetExistentSites()
BEGIN
    SELECT SiteName FROM SiteMaster;
END//
DELIMITER ;


---- EXCEL's ---- 

DROP PROCEDURE IF EXISTS sp_Bulk_Excel_GetLinkTypes;
DELIMITER //
CREATE PROCEDURE sp_Bulk_Excel_GetLinkTypes()
BEGIN
    SELECT 
        LinkTypeID as Id, LinkTypeName as Name, IsActive
    FROM 
        LinkTypeMaster
    WHERE 
        IsActive  = 1 AND IsDelete = 0
    ORDER BY 
        LinkTypeName ASC
END//
DELIMITER ;


DROP PROCEDURE IF EXISTS sp_Bulk_Excel_GetSiteTypes;
DELIMITER //
CREATE PROCEDURE sp_Bulk_Excel_GetSiteTypes()
BEGIN
    SELECT 
        SiteTypeID as Id, SiteTypeName as Name, IsActive
    FROM 
        SiteTypeMaster
    WHERE 
        IsActive = 1 AND IsDelete = 0
    ORDER BY 
        SiteTypeName ASC
END//
DELIMITER ;

-- Counties
DROP PROCEDURE IF EXISTS sp_Bulk_Dropdown_Counties;
DELIMITER //
CREATE PROCEDURE sp_Bulk_Dropdown_Counties()
BEGIN
    SELECT CountyId, CountyName FROM County_Master ORDER BY CountyName ASC
END //
DELIMITER ;

-- SubCounties
DROP PROCEDURE IF EXISTS sp_Bulk_Dropdown_SubCounties;
DELIMITER //
CREATE PROCEDURE sp_Bulk_Dropdown_SubCounties()
BEGIN
SELECT SubCountyID as SubCountyId, SubCountyName, CountyID as CountyId FROM SubCountyMaster ORDER BY SubCountyName ASC
END //
DELIMITER ;

-- Constituencies
DROP PROCEDURE IF EXISTS sp_Bulk_Dropdown_Constituencies;
DELIMITER //
CREATE PROCEDURE sp_Bulk_Dropdown_Constituencies()
BEGIN
    SELECT ConstituencyId, ConstituencyName, SubCountyID as SubCountyId FROM ConstituencyMaster ORDER BY ConstituencyName ASC
END //
DELIMITER ;

-- Wards
DROP PROCEDURE IF EXISTS sp_Bulk_Dropdown_Wards;
DELIMITER //
CREATE PROCEDURE sp_Bulk_Dropdown_Wards()
BEGIN
    SELECT WardID as WardId, WardName, ConstituencyId FROM WardMaster ORDER BY WardName ASC
END //
DELIMITER ;

-- LinkType Dropdown
DROP PROCEDURE IF EXISTS sp_Bulk_Dropdown_LinkTypes;
DELIMITER //
CREATE PROCEDURE sp_Bulk_Dropdown_LinkTypes()
BEGIN
SELECT LinkTypeID as Id, LinkTypeName as Name, IsActive
            FROM LinkTypeMaster
            WHERE IsActive  = 1 AND IsDelete = 0
            ORDER BY LinkTypeName ASC
END//
DELIMITER ;

-- SiteType Dropdown
DROP PROCEDURE IF EXISTS sp_Bulk_Dropdown_SiteTypes;
DELIMITER //
CREATE PROCEDURE sp_Bulk_Dropdown_SiteTypes()
BEGIN
SELECT SiteTypeID as Id, SiteTypeName as Name, IsActive
            FROM SiteTypeMaster
            WHERE IsActive = 1 AND IsDelete = 0
            ORDER BY SiteTypeName ASC
END //
DELIMITER ;