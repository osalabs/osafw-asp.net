DROP TABLE users;
CREATE TABLE users (
  id int IDENTITY(1,1) PRIMARY KEY CLUSTERED,

  email                 NVARCHAR(128) NOT NULL DEFAULT '',
  pwd                   NVARCHAR(32) NOT NULL DEFAULT '',
  access_level          TINYINT NOT NULL,  /*0 - usual user, 80 - moderator, 100 - admin*/

  fname                 NVARCHAR(32) NOT NULL DEFAULT '',
  lname                 NVARCHAR(32) NOT NULL DEFAULT '',

  address1              NVARCHAR(64) NOT NULL DEFAULT '',
  address2              NVARCHAR(64) NOT NULL DEFAULT '',
  city                  NVARCHAR(64) NOT NULL DEFAULT '',
  state                 NVARCHAR(4) NOT NULL DEFAULT '',
  zip                   NVARCHAR(16) NOT NULL DEFAULT '',
  phone                 NVARCHAR(16) NOT NULL DEFAULT '',

  notes                 NTEXT,
  login_time            DATETIME2,

  status                TINYINT DEFAULT 0,        /*0-ok, 127-deleted*/
  add_time              DATETIME2 NOT NULL DEFAULT getdate(),
  add_users_id          INT DEFAULT 0,
  upd_time              DATETIME2,
  upd_users_id          INT DEFAULT 0
)
CREATE UNIQUE INDEX users_email ON users (email);
INSERT INTO users (fname, lname, email, pwd, access_level)
VALUES ('Website','Admin','admin@admin.com','CHANGE_ME',100);


/*Site Settings - special table for misc site settings*/
DROP TABLE settings;
CREATE TABLE settings (
  id int IDENTITY(1,1) PRIMARY KEY CLUSTERED,
  icat                  NVARCHAR(64) NOT NULL DEFAULT '', /*settings category: ''-system, 'other' -site specific*/
  icode                 NVARCHAR(64) NOT NULL DEFAULT '', /*settings internal code*/
  ivalue                NVARCHAR(MAX) NOT NULL DEFAULT '', /*value*/

  iname                 NVARCHAR(64) NOT NULL DEFAULT '', /*settings visible name*/
  idesc                 NVARCHAR(MAX),                    /*settings visible description*/
  input                 TINYINT NOT NULL default 0,       /*form input type: 0-input, 10-textarea, 20-select, 21-select multi, 30-checkbox, 40-radio, 50-date*/
  allowed_values        NVARCHAR(MAX),                    /*space-separated values, use &nbsp; for space, used for: select, select multi, checkbox, radio*/

  is_user_edit          TINYINT DEFAULT 0,  /* if 1 - use can edit this value*/

  add_time              DATETIME2 NOT NULL DEFAULT getdate(),
  add_users_id          INT DEFAULT 0,
  upd_time              DATETIME2,
  upd_users_id          INT DEFAULT 0
);
CREATE UNIQUE INDEX settings_icode ON settings (icode);
CREATE INDEX settings_icat ON settings (icat);
INSERT INTO settings (is_user_edit, input, icat, icode, ivalue, iname, idesc) VALUES
(1, 10, '', 'test', 'novalue', 'test settings', 'description');

/* upload categories */
DROP TABLE att_categories;
CREATE TABLE att_categories (
  id int IDENTITY(1,1) PRIMARY KEY CLUSTERED,

  icode                 NVARCHAR(64) NOT NULL DEFAULT '', /*to use from code*/
  iname                 NVARCHAR(64) NOT NULL DEFAULT '',
  idesc                 NTEXT,
  prio                  INT NOT NULL DEFAULT 0,     /* 0 is normal and lowest priority*/

  status                TINYINT DEFAULT 0,        /*0-ok, 127-deleted*/
  add_time              DATETIME2 NOT NULL DEFAULT getdate(),
  add_users_id          INT DEFAULT 0,
  upd_time              DATETIME2,
  upd_users_id          INT DEFAULT 0
);
INSERT INTO att_categories (icode, iname) VALUES
('general', 'General images')
,('users', 'Member photos')
,('files', 'Files')
,('spage_banner', 'Page banners')
;

DROP TABLE att;
CREATE TABLE att (
  id int IDENTITY(1,1) PRIMARY KEY CLUSTERED, /* files stored on disk under 0/0/0/id.dat */
  att_categories_id       INT NULL FOREIGN KEY REFERENCES att_categories(id),

  table_name            NVARCHAR(128) NOT NULL DEFAULT '',
  item_id               INT NOT NULL DEFAULT 0,

  is_inline             TINYINT DEFAULT 0, /* if uploaded with wysiwyg */
  is_image              TINYINT DEFAULT 0, /* 1 if this is supported image */

  fname                 NVARCHAR(255) NOT NULL DEFAULT '',              /*original file name*/
  fsize                 INT DEFAULT 0,                   /*file size*/
  ext                   NVARCHAR(16) NOT NULL DEFAULT '',                 /*extension*/
  iname                 NVARCHAR(255) NOT NULL DEFAULT '',   /*attachment name*/

  status                TINYINT DEFAULT 0,        /*0-ok, 1-under upload, 127-deleted*/
  add_time              DATETIME2 NOT NULL DEFAULT getdate(),
  add_users_id          INT DEFAULT 0,
  upd_time              DATETIME2,
  upd_users_id          INT DEFAULT 0
);
CREATE INDEX att_table_name ON att (table_name, item_id);

/* link att files to table items*/
DROP TABLE att_table_link;
CREATE TABLE att_table_link (
  id int IDENTITY(1,1) PRIMARY KEY CLUSTERED,
  att_id                INT NOT NULL,

  table_name            NVARCHAR(128) NOT NULL DEFAULT '',
  item_id               INT NOT NULL,

  status                TINYINT DEFAULT 0,        /*0-ok, 1-under update*/
  add_time              DATETIME2 NOT NULL DEFAULT getdate(),
  add_users_id          INT DEFAULT 0,
);
CREATE UNIQUE INDEX att_table_link_UX ON att_table_link (table_name, item_id, att_id);

/*Static pages*/
DROP TABLE spages;
CREATE TABLE spages (
  id int IDENTITY(1,1) PRIMARY KEY CLUSTERED,
  parent_id             INT NOT NULL DEFAULT 0,  /*parent page id*/

  url                   NVARCHAR(255) NOT NULL DEFAULT '',      /*sub-url from parent page*/
  iname                 NVARCHAR(64) NOT NULL DEFAULT '',       /*page name-title*/
  idesc                 NVARCHAR(MAX),                          /*page contents, markdown*/
  head_att_id           INT NULL FOREIGN KEY REFERENCES att(id), /*optional head banner image*/

  idesc_left            NVARCHAR(MAX),                          /*left sidebar content, markdown*/
  idesc_right           NVARCHAR(MAX),                          /*right sidebar content, markdown*/
  meta_keywords         NVARCHAR(255) NOT NULL DEFAULT '',      /*meta keywords*/
  meta_description      NVARCHAR(255) NOT NULL DEFAULT '',      /*meta description*/

  pub_time              DATETIME2,                               /*publish date-time*/
  template              NVARCHAR(64),                           /*template to use, if not defined - default site template used*/
  prio                  INT NOT NULL DEFAULT 0,                 /* 0 is normal and lowest priority*/
  is_home               INT DEFAULT 0,                          /* 1 is for home page (non-deletable page*/

  custom_css            NVARCHAR(MAX),                          /*custom page css*/
  custom_js             NVARCHAR(MAX),                          /*custom page js*/

  status                TINYINT DEFAULT 0,    /*0-ok, 10-not published, 127-deleted*/
  add_time              DATETIME2 NOT NULL DEFAULT getdate(),
  add_users_id          INT DEFAULT 0,
  upd_time              DATETIME2,
  upd_users_id          INT DEFAULT 0
);
CREATE INDEX spages_parent_id ON spages (parent_id, prio);
CREATE INDEX spages_url ON spages (url);
GO

--TRUNCATE TABLE spages;
INSERT INTO spages (parent_id, url, iname) VALUES
(0,'','Home') --1
,(1,'test','Test sub-home page') --2
;
update spages set is_home=1 where id=1;


/* some categories */
DROP TABLE categories;
CREATE TABLE categories (
  id int IDENTITY(1,1) PRIMARY KEY CLUSTERED,

  iname					        NVARCHAR(64) NOT NULL DEFAULT '',
  idesc					        NTEXT,
  prio                  INT NOT NULL DEFAULT 0,     /* 0 is normal and lowest priority*/

  status                TINYINT DEFAULT 0,        /*0-ok, 1-under upload, 127-deleted*/
  add_time              DATETIME2 NOT NULL DEFAULT getdate(),
  add_users_id          INT DEFAULT 0,
  upd_time              DATETIME2,
  upd_users_id          INT DEFAULT 0
);
INSERT INTO categories (iname) VALUES
('category1')
,('category2')
,('category3')
;


/*event types for log*/
DROP TABLE events;
CREATE TABLE events (
  id INT IDENTITY(1,1) PRIMARY KEY CLUSTERED,
  icode                 NVARCHAR(64) NOT NULL default '',

  iname                 NVARCHAR(255) NOT NULL default '',
  idesc                 NTEXT,

  status                TINYINT DEFAULT 0,        /*0-ok, 127-deleted*/
  add_time              DATETIME2 NOT NULL DEFAULT getdate(),
  add_users_id          INT DEFAULT 0,
  upd_time              DATETIME2,
  upd_users_id          INT DEFAULT 0
);
CREATE UNIQUE INDEX events_icode_idx ON events (icode);
INSERT INTO events (icode, iname) VALUES ('login',    'User login');
INSERT INTO events (icode, iname) VALUES ('logoff',   'User logoff');
INSERT INTO events (icode, iname) VALUES ('chpwd',    'User changed login/pwd');
INSERT INTO events (icode, iname) VALUES ('users_add',    'New user added');
INSERT INTO events (icode, iname) VALUES ('users_upd',    'User updated');
INSERT INTO events (icode, iname) VALUES ('users_del',    'User deleted');

/* log of all user-initiated events */
DROP TABLE event_log;
CREATE TABLE event_log (
  id BIGINT IDENTITY(1,1) PRIMARY KEY CLUSTERED,
  events_id        INT NOT NULL DEFAULT 0,           /* email type sent */

  item_id               INT NOT NULL DEFAULT 0,           /*related id*/
  item_id2              INT NOT NULL DEFAULT 0,           /*related id (if another)*/

  iname                 NVARCHAR(255) NOT NULL DEFAULT '', /*short description of what's happened or additional data*/

  records_affected      INT NOT NULL DEFAULT 0,
  fields                NVARCHAR(MAX),       /*serialized json with related fields data (for history) in form {fieldname: data, fieldname: data}*/

  add_time              DATETIME2 NOT NULL DEFAULT getdate(),  /*date record added*/
  add_users_id          INT DEFAULT 0,                        /*user added record, 0 if sent by cron module*/
);
CREATE INDEX event_log_events_id_idx ON event_log (events_id);
CREATE INDEX event_log_add_users_id_dx ON event_log (add_users_id)
CREATE INDEX event_log_add_time_idx ON event_log (add_time);

/*Lookup Manager Tables*/
DROP TABLE lookup_manager_tables;
CREATE TABLE lookup_manager_tables (
  id INT IDENTITY(1,1) PRIMARY KEY CLUSTERED,

  tname                 NVARCHAR(255) NOT NULL DEFAULT '', /*table name*/
  iname                 NVARCHAR(255) NOT NULL DEFAULT '', /*human table name*/
  idesc                 NVARCHAR(MAX),                     /*table internal description*/

  is_one_form           TINYINT NOT NULL DEFAULT 0,        /*1 - lookup table cotains one row, use form view*/
  is_custom_form        TINYINT NOT NULL DEFAULT 0,        /*1 - use custom form template, named by lowercase(tname)*/
  header_text           NVARCHAR(MAX),                     /*text to show in header when editing table*/
  footer_text           NVARCHAR(MAX),                     /*text to show in footer when editing table*/
  column_id             NVARCHAR(255),                     /*table id column, if empty - use id*/

  list_columns          NVARCHAR(MAX),                     /*comma-separated field list to display on list view, if defined - bo table edit mode available*/
  columns               NVARCHAR(MAX),                     /*comma-separated field list to display, if empty - all fields displayed*/
  column_names          NVARCHAR(MAX),                     /*comma-separated column list of column names, if empty - use field name*/
  column_types          NVARCHAR(MAX),                     /*comma-separated column list of column types/lookups (" "-string(default),textarea,checkbox,tname.field-lookup table), if empty - use standard input[text]*/
  column_groups         NVARCHAR(MAX),                     /*comma-separated column list of groups column related to, if empty - don't include column in group*/

  status                TINYINT DEFAULT 0,                /*0-ok, 127-deleted*/
  add_time              DATETIME2 NOT NULL DEFAULT getdate(),  /*date record added*/
  add_users_id          INT DEFAULT 0,                        /*user added record*/
  upd_time              DATETIME2,
  upd_users_id          INT DEFAULT 0
);
CREATE UNIQUE INDEX lookup_manager_tables_tname ON lookup_manager_tables (tname);
GO

insert into lookup_manager_tables (tname, iname) VALUES
('events','Events')
, ('categories','Categories');
GO