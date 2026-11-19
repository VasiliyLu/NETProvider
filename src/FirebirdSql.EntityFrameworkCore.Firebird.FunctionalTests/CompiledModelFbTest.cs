/*
 *    The contents of this file are subject to the Initial
 *    Developer's Public License Version 1.0 (the "License");
 *    you may not use this file except in compliance with the
 *    License. You may obtain a copy of the License at
 *    https://github.com/FirebirdSQL/NETProvider/raw/master/license.txt.
 *
 *    Software distributed under the License is distributed on
 *    an "AS IS" basis, WITHOUT WARRANTY OF ANY KIND, either
 *    express or implied. See the License for the specific
 *    language governing rights and limitations under the License.
 *
 *    All Rights Reserved.
 */

using System;
using System.Linq;
using FirebirdSql.Data.FirebirdClient;
using FirebirdSql.EntityFrameworkCore.Firebird.Design.Internal;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Scaffolding;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace FirebirdSql.EntityFrameworkCore.Firebird.FunctionalTests;

public class CompiledModelFbTest
{
	// Only the NativeAOT model writes type mappings out, as Default.Clone(...)
	[Fact]
	public void NativeAot_compiled_model_can_be_generated_for_all_type_mappings()
	{
		using var context = new AllTypesContext();
		var model = context.GetService<IDesignTimeModel>().Model;

		var services = new ServiceCollection().AddEntityFrameworkDesignTimeServices();
		new FbDesignTimeServices().ConfigureDesignTimeServices(services);
		services.AddDbContextDesignTimeServices(context);
		var options = new CompiledModelCodeGenerationOptions
		{
			ContextType = typeof(AllTypesContext),
			ModelNamespace = "TestNamespace",
			Language = "C#",
			UseNullableReferenceTypes = true,
			ForNativeAot = true,
		};
		var generator = services.BuildServiceProvider()
			.GetRequiredService<ICompiledModelCodeGeneratorSelector>()
			.Select(options);

		var files = generator.GenerateModel(model, options);

		Assert.Contains(files, f => f.Code.Contains("FbStringTypeMapping.Default"));
	}

	[Theory]
	[InlineData(nameof(AllTypes.VarChar), FbDbType.VarChar)]
	[InlineData(nameof(AllTypes.Char), FbDbType.Char)]
	[InlineData(nameof(AllTypes.Text), FbDbType.Text)]
	[InlineData(nameof(AllTypes.TimeStamp), FbDbType.TimeStamp)]
	[InlineData(nameof(AllTypes.Date), FbDbType.Date)]
	[InlineData(nameof(AllTypes.Time), FbDbType.Time)]
	public void Mapping_cloned_from_Default_keeps_FbDbType(string propertyName, FbDbType expected)
	{
		using var context = new AllTypesContext();
		var mapping = (RelationalTypeMapping)context.Model.FindEntityType(typeof(AllTypes))!.FindProperty(propertyName)!.GetTypeMapping();

		// What the generated compiled model does
		var @default = (RelationalTypeMapping)mapping.GetType().GetProperty("Default")!.GetValue(null)!;
		var clone = @default.Clone(new RelationalTypeMappingInfo(storeTypeName: mapping.StoreType, size: mapping.Size));

		Assert.Equal(expected, GetFbDbType(mapping));
		Assert.Equal(expected, GetFbDbType(clone));
	}

	[Fact]
	public void All_provider_type_mappings_have_Default()
	{
		using var context = new AllTypesContext();
		var mappingTypes = context.Model.FindEntityType(typeof(AllTypes))!.GetProperties()
			.Select(p => p.GetTypeMapping().GetType())
			.Where(t => t.Namespace!.StartsWith("FirebirdSql.", StringComparison.Ordinal))
			.Distinct();

		Assert.All(mappingTypes, t =>
		{
			var defaultProperty = t.GetProperty("Default");
			Assert.NotNull(defaultProperty);
			Assert.True(defaultProperty.GetMethod!.IsStatic && defaultProperty.GetMethod.IsPublic);
			Assert.Equal(t, defaultProperty.PropertyType);
		});
	}

	static FbDbType GetFbDbType(RelationalTypeMapping mapping)
		=> ((FbParameter)mapping.CreateParameter(new FbCommand(), "p", null)).FbDbType;

	class AllTypesContext : DbContext
	{
		protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
			=> optionsBuilder.UseFirebird("database=localhost:_.fdb;user=sysdba;password=masterkey;charset=utf8");

		protected override void OnModelCreating(ModelBuilder modelBuilder)
		{
			modelBuilder.Entity<AllTypes>(e =>
			{
				e.Property(x => x.VarChar).HasMaxLength(100);
				e.Property(x => x.Char).HasColumnType("CHAR(10)");
				e.Property(x => x.Text).HasColumnType("BLOB SUB_TYPE TEXT");
				e.Property(x => x.Date).HasColumnType("DATE");
				e.Property(x => x.Decimal).HasPrecision(15, 4);
			});
		}
	}

	class AllTypes
	{
		public int Id { get; set; }
		public string VarChar { get; set; }
		public string Char { get; set; }
		public string Text { get; set; }
		public bool Bool { get; set; }
		public short Short { get; set; }
		public long Long { get; set; }
		public float Float { get; set; }
		public double Double { get; set; }
		public decimal Decimal { get; set; }
		public DateTime TimeStamp { get; set; }
		public DateTime Date { get; set; }
		public DateOnly DateOnly { get; set; }
		public TimeOnly TimeOnly { get; set; }
		public TimeSpan Time { get; set; }
		public Guid Guid { get; set; }
		public byte[] Binary { get; set; }
	}
}
