/*Demo table*/
DROP TABLE demos;
CREATE TABLE demos (
  id INT IDENTITY(1,1) PRIMARY KEY CLUSTERED,
  parent_id             INT NOT NULL DEFAULT 0,           /*parent id - combo selection from SQL*/
  demo_dicts_id         INT NOT NULL DEFAULT 0,           /* demo dictionary link*/

  iname                 NVARCHAR(64) NOT NULL DEFAULT '', /*string value for names*/
  idesc                 NVARCHAR(MAX),                    /*large text value*/

  email                 NVARCHAR(128) NOT NULL DEFAULT '',/*string value for unique field, such as email*/

  fint                  INT NOT NULL DEFAULT 0,           /*accept only INT*/
  ffloat                FLOAT NOT NULL DEFAULT 0,         /*accept float digital values*/

  dict_link_auto_id     INT NOT NULL DEFAULT 0,           /*index of autocomplete field - linked to demo_dicts*/
  dict_link_multi       NVARCHAR(255) NOT NULL DEFAULT '',    /*multiple select values, link to demo_dicts*/

  fcombo                INT NOT NULL DEFAULT 0,           /*index of combo selection*/
  fradio                INT NOT NULL DEFAULT 0,           /*index of radio selection*/
  fyesno                TINYINT NOT NULL DEFAULT 0,       /*yes/no field 0 - NO, 1 - YES*/
  is_checkbox           TINYINT NOT NULL DEFAULT 0,       /*checkbox field 0 - not set, 1 - set*/

  fdate_combo           DATE,                             /*date field with 3 combos editing*/
  fdate_pop             DATE,                             /*date field with popup editing*/
  fdatetime             DATETIME2,                         /*date+time field*/
  ftime                 INT NOT NULL DEFAULT 0,           /*time field - we always store time as seconds from start of the day [0-86400]*/

  status                TINYINT DEFAULT 0,        /*0-ok, 127-deleted*/
  add_time              DATETIME2 NOT NULL DEFAULT getdate(),  /*date record added*/
  add_user_id           INT DEFAULT 0,                        /*user added record*/
  upd_time              DATETIME2,
  upd_user_id           INT DEFAULT 0
);
CREATE UNIQUE INDEX demos_email ON demos (email);

/*Demo Dictionary table*/
DROP TABLE demo_dicts;
CREATE TABLE demo_dicts (
  id INT IDENTITY(1,1) PRIMARY KEY CLUSTERED,

  iname                 NVARCHAR(64) NOT NULL default '',
  idesc                 NVARCHAR(MAX),

  status                TINYINT DEFAULT 0,        /*0-ok, 1-under upload, 127-deleted*/
  add_time              DATETIME2 NOT NULL DEFAULT getdate(), /*from date_joined*/
  add_user_id           INT DEFAULT 0,
  upd_time              DATETIME2,
  upd_user_id           INT DEFAULT 0
);
INSERT INTO demo_dicts (iname, idesc, add_time) VALUES ('test1', 'test1 description', GETDATE());
INSERT INTO demo_dicts (iname, idesc, add_time) VALUES ('test2', 'test2 description', GETDATE());
INSERT INTO demo_dicts (iname, idesc, add_time) VALUES ('test3', 'test3 description', GETDATE());
