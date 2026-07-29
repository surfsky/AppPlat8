using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Internal;
using Microsoft.EntityFrameworkCore.Migrations.Internal;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Reflection;
using System.Text;
using System.Linq;
using Microsoft.EntityFrameworkCore.Design.Internal;
using Microsoft.EntityFrameworkCore.Migrations.Design;

namespace App.DAL
{
    /// <summary>
    /// 根据模型自动更新数据库表（似乎并未生效）
    /// https://www.cnblogs.com/jRoger/p/entity-framework-core-auto-migration.html
    /// </summary>
    public class AutoMigration
    {
        private readonly IServiceProvider _serviceProvider;
        private AppPlatContext _context;

        public AutoMigration(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
            _context = serviceProvider.GetService<AppPlatContext>();
        }

        public void Migrator()
        {
            var path = Path.Combine(AppContext.BaseDirectory, "..\\..\\..\\Migrations\\");
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }
            else
            {
                Directory.GetFiles(path).ToList().ForEach(File.Delete);
            }

            using (_context)
            {
                var services = ((IInfrastructure<IServiceProvider>)_context).Instance;
                var codeHelper = new CSharpHelper();
                var scaffolder = ActivatorUtilities.CreateInstance<MigrationsScaffolder>(services,
                    new CSharpMigrationsGenerator(codeHelper, new CSharpMigrationOperationGenerator(codeHelper),
                        new CSharpSnapshotGenerator(codeHelper)));

                var projectDir = Path.Combine(path, "..\\");
                var migrationAssembly = new MigrationsAssembly(new CurrentDbContext(_context), _context.Options, new MigrationsIdGenerator());
                scaffolder.GetType().GetField("_migrationsAssembly", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(scaffolder, migrationAssembly);

                var readonlyDic = new ReadOnlyDictionary<string, TypeInfo>(new Dictionary<string, TypeInfo>());
                migrationAssembly.GetType().GetField("_migrations", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(migrationAssembly, new LazyRef<IReadOnlyDictionary<string, TypeInfo>>(readonlyDic));
                var migration = scaffolder.ScaffoldMigration("Information.Migrations", "Information");

                scaffolder.Save(projectDir, migration, path);

                //另外一种保存方式
                //File.WriteAllText($"Migrations\\{migration.MigrationId}{migration.FileExtension}", migration.MigrationCode);
                //File.WriteAllText("Migrations\\" +
                //    migration.MigrationId + ".Designer" + migration.FileExtension,
                //    migration.MetadataCode);
                //File.WriteAllText("Migrations\\" + migration.SnapshotName + migration.FileExtension,
                //    migration.SnapshotCode);
            }

            using (_context = (AppPlatContext)_serviceProvider.GetService<IDbContext>())
            {
                _context.Database.Migrate();
            }
        }
    }
}
