# osafw-asp.net
ASP.NET web framework in pure VB.NET code

Created as simplified and lightweight alternative to other ASP.NET frameworks like ASP.NET MVC

![image](https://user-images.githubusercontent.com/1141095/51185501-e952e700-189c-11e9-8b8e-902f40499d85.png)

## Features
- simple and straightforward in development and maintenance
- MVC-like - code, data, templates are split
  - code consists of: controllers, models, framework core and optional 3rd party libs
  - uses [ParsePage template engine](https://github.com/osalabs/parsepage)
  - data stored by default in SQL Server database [using db.net](https://github.com/osalabs/db.net)
- RESTful with some practical enhancements
- integrated auth - simple flat access levels auth
- UI based on [Bootstrap 4](http://getbootstrap.com) with minimal custom CSS - it's easy to customzie or apply your own theme
- use of well-known 3rd party libraries: [jQuery](http://jquery.com), [jQuery Form](https://github.com/malsup/form), jGrowl, markdown libs, etc...

## Demo
http://demo.engineeredit.com/ - this is how it looks in action right after installation before customizations

## Installation
1. put contents of `/www` into your webserver's public html folder
2. edit `/www/web.config` for db settings
3. create database from `/www/App_Data/sql/database.sql`
4. open site in your browser and login with credentials as defined in database.sql (remember to change password)

## Documentation

### REST mappings
Controllers automatically directly mapped to URLs, so developer doesn't need to write routing rules:

  - `GET /Controller` - list view `IndexAction()`
  - `GET /Controller/ID` - one record view `ShowAction()`
  - `GET /Controller/ID/new` - one record new form `ShowFormAction()`
  - `GET /Controller/ID/edit` - one record edit form `ShowFormAction()`
  - `GET /Controller/ID/delete` - one record delete confirmation form `ShowDeleteAction()`
  - `POST /Controller` - insert new record `SaveAction()`
  - `PUT /Controller` - update multiple records `SaveMultiAction()`
  - `POST/PUT /Controller/ID` - update record `SaveAction()`
  - `POST/DELETE /Controller/ID` - delete record ($_POST should be empty) `DeleteAction()`
  - `GET/POST /Controller/(Something)[/ID]` - call for arbitrary action from the controller `SomethingAction()`

For example `GET /Products` will call `ProductsController.IndexAction()`
And this will cause rendering templates from `/www/App_Data/templates/products/index`

## Development
1. in Visual Studio do `File -> Open -> Web Site...` (Shift+Alt+O)
2. press Ctrl+F5 to run
3. review debug log in `/www/App_Data/main.log`
4. edit or create new controllers and models in `/www/App_Code/controllers` and `/www/App_Code/models`
5. modify templates in `/www/App_Data/template`
