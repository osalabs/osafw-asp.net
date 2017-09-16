# osafw-asp.net
ASP.NET web framework in pure VB.NET code

Created as simplified and lightweight alternative to other ASP.NET frameworks like ASP.NET MVC

## Features
- simple and straight in development and maintenance
- **MVC-like** code, data, templates are split
  - code consists of: controllers, models, framework core and optional 3rd party libs
  - uses [ParsePage template engine](https://github.com/osalabs/parsepage)
  - data stored by default in SQL Server database [using db.net](https://github.com/osalabs/db.net)
- **RESTful** with some practical enhancements
  - `GET /Controller` - list view
  - `GET /Controller/ID` - one record view
  - `GET /Controller/ID/new` - one record new form 
  - `GET /Controller/ID/edit` - one record edit form 
  - `GET /Controller/ID/delete` - one record delete confirmation form 
  - `POST /Controller` - insert new record
  - `PUT /Controller` - update multiple records
  - `POST/PUT /Controller/ID` - update record
  - `POST/DELETE /Controller/ID` - delete record ($_POST should be empty)
  - `GET/POST /Controller/(Action)[/ID]` - call for arbitrary action from the controller
- integrated auth - simple flat access levels auth
- use of well-known 3rd party libraries
  - [jQuery](http://jquery.com)
  - [Twitter Bootstrap 3](http://getbootstrap.com)
  - [jQuery Form](https://github.com/malsup/form)
  - jGrowl
  - markdown libs
  - others... (TODO)

## Demo

http://demo.engineeredit.com/ - this is how it looks in action right after installation before customizations

## Installation

1. put contents of `/www` into your webserver's public html folder
2. edit `/www/web.config` for db settings
3. create database from `/db/database.sql`
4. open site in your browser and login with credentials as defined in database.sql

Automatied install via Nuget - TDB

## Documentation

TODO

