для запуска во время разработки необходимо выполнить команду: dotnet run
для компиляции в exe необходимо выполнить команду: dotnet build
для указания FTP: https://github.com/JoVlaX/VantuzLauncher -> Settings -> категория "Security and quality" - "Secrets and variables" -> "Actions" -> "New repository secret" (если еще нету) 
-> Создаешь три разных параметра: 
Name: SFTP_HOST — Secret: [твой IP/домен]
Name: SFTP_USERNAME — Secret: [логин хостинга]
Name: SFTP_PASSWORD — Secret: [пароль хостинга]
