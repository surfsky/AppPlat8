# PostgreSQL

下载：https://www.pgadmin.org/download/
会安装命令行工具 psql 和图形化管理工具 pgAdmin 4。
pgAdmin 4，这是 PostgreSQL 的官方图形化管理工具，支持 Windows、macOS 和 Linux。
安装完毕后，打开 pgAdmin，输入连接信息（主机地址、端口号、用户名、密码）连接到 PostgreSQL 数据库服务器。
默认数据库：PostgreSQL 采用的是多文件存储结构，而不是像某些数据库那样把所有数据存在一个大文件里。
    # 切换到 postgres 用户
    sudo -u postgres -i
    # 然后再进入目录
    cd /Library/PostgreSQL/18/data

## 本机设置

默认端口号：5432，已改为 5050
密码：Pa******d


## 界面化管理

用pgAdmin 就行了，可以调整为中文模式，管理方式类似 Oracle。
表新建：在 pgAdmin 中，点击打开数据库 -> 架构Schema -> public -> 表 -> 右键新建表
表管理：表 -> 右键属性 -> 可以调整表结构、索引、约束等。
表数据：表 -> 右键查看/编辑数据 -> 可以直接查看、新增、修改、删除数据。
备份：数据库或表 -> 右键备份 -> 可以选择备份格式、对象类型、文件路径等。
自动备份：
     pgAgent 计划任务 -> 设置命令：/Library/PostgreSQL/18/pgAdmin 4.app/Contents/SharedSupport/pg_dump --file "/Users/CJH/Downloads/DevDb/Backup/bak1" --host "localhost" --port "5432" --username "postgres" --no-password --format=c --large-objects --verbose "postgres"
     设置每日0点0分执行备份任务。

## 命令行

创建数据库：
createdb -U postgres your_database_name

创建表：
psql -U postgres -d your_database_name -c "CREATE TABLE table_name (id SERIAL PRIMARY KEY, name VARCHAR(255), age INTEGER);"

插入数据：
psql -U postgres -d your_database_name -c "INSERT INTO table_name (name, age) VALUES ('张三', 25);"

查询数据：
psql -U postgres -d your_database_name -c "SELECT * FROM table_name;"

备份：
pg_dump -U postgres -d your_database_name -f backup_file.sql
pg_dump -U postgres -d your_database_name -t table_name -f table_backup.sql

恢复：
pg_restore -U postgres -d your_database_name -f backup_file.sql


删除表：
psql -U postgres -d your_database_name -c "DROP TABLE table_name;"

删除数据库：
dropdb -U postgres your_database_name
