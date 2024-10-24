Database Explorer
This project is a Database Explorer application developed using WPF (Windows Presentation Foundation). The application allows users to connect to a database, explore its structure, and modify data within the tables.

Features
Connection String Input: Users can connect to a database by providing a connection string.
Database Name Display: Once connected, the application's interface displays the name of the connected database.
Table and Column Management: Users can view the list of tables and columns in the database.
Data Editing: The application allows users to browse table rows and make edits directly to the data.
CRUD Operations: Users can perform Create, Read, Update, and Delete operations on the database rows.

Prerequisites
.NET Core SDK (version 5.0 or later)
SQL Server or any other compatible database server

Installation
Clone the repository:
git clone https://github.com/yourusername/DatabaseExplorer.git

Open the project in Visual Studio.
Build the solution to restore dependencies and compile the application.

Usage
Launch the application.
Enter the connection string for your database in the provided input field.
Connect to the database by clicking the "Connect" button.
Explore the database:
The application will display the name of the connected database.
You can view the list of tables and their columns.
Select a table to browse its rows.
Edit data as needed:
Double-click a cell to edit its value.
Use the context menu or toolbar to add, update, or delete rows.

Example Connection String
For a SQL Server database:

Server=myServerAddress;Database=myDataBase;User Id=myUsername;Password=myPassword;

Contributing
Contributions are welcome! Please open a pull request or issue to suggest changes or report bugs.

License
This project is licensed under the MIT License.
