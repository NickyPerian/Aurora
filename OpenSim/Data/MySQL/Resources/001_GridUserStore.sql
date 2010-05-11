BEGIN;

CREATE TABLE `GridUser` (
    `UserID` VARCHAR(255) NOT NULL,
    `HomeRegionID` CHAR(36) NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000',
    `HomePosition` CHAR(64) NOT NULL DEFAULT '<0,0,0>',
    `HomeLookAt` CHAR(64) NOT NULL DEFAULT '<0,0,0>',
    `LastRegionID` CHAR(36) NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000',
    `LastPosition` CHAR(64) NOT NULL DEFAULT '<0,0,0>',
    `LastLookAt` CHAR(64) NOT NULL DEFAULT '<0,0,0>',
    `Online` CHAR(5) NOT NULL DEFAULT 'false',
    `Login` CHAR(16) NOT NULL DEFAULT '0',
    `Logout` CHAR(16) NOT NULL DEFAULT '0',
    PRIMARY KEY  (`UserID`)
) ENGINE=InnoDB;

COMMIT;