using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Xunit;
using Ecom.Core.Entities.Tbl_Product;
using Ecom.Core.Exceptions;
using ECom.Infrastructure.Data;
using ECom.Infrastructure.Repositories;

namespace ECom.Api.Tests.Repositories
{
    public class RepositoryTests
    {
        private static AppDbContext NewContext()
        {
            var o = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase("db-" + Guid.NewGuid().ToString("N"))
                .Options;
            return new AppDbContext(o);
        }

        // AAA: Arrange below. Simulated store with a fresh CategoryRepo.
        [Fact]
        public async Task CategoryRepo_AddAsync_AddsAndPersists()
        {
            // ---- Arrange ----
            using var db = NewContext();
            var repo = new CategoryRepo(db);
            var cat = new TblCategories { Name = "AAA-Added", Description = "d" };

            // ---- Act ----
            var added = await repo.AddAsync(cat);
            await db.SaveChangesAsync();

            // ---- Assert ----
            Assert.NotNull(added);
            Assert.True(added.Id > 0);
            Assert.NotNull(await repo.GetByIdAsync(added.Id));
        }

        [Fact]
        public async Task CategoryRepo_GetByIdOrThrowAsync_WhenFound_ShouldReturnEntity()
        {
            // ---- Arrange ----
            using var db = NewContext();
            var repo = new CategoryRepo(db);
            var cat = new TblCategories { Name = "AAA-Throw", Description = "d" };
            await repo.AddAsync(cat);
            await db.SaveChangesAsync();

            // ---- Act ----
            var loaded = await repo.GetByIdOrThrowAsync(cat.Id);

            // ---- Assert ----
            Assert.NotNull(loaded);
            Assert.Equal(cat.Id, loaded.Id);
        }

        [Fact]
        public async Task CategoryRepo_GetByIdOrThrowAsync_WhenMissing_ShouldThrowNotFound()
        {
            // ---- Arrange ----
            using var db = NewContext();
            var repo = new CategoryRepo(db);

            // ---- Act / Assert ----
            var ex = await Assert.ThrowsAsync<NotFoundException>(() => repo.GetByIdOrThrowAsync(999));
            Assert.Contains("TblCategories", ex.Message);
        }

        [Fact]
        public async Task CategoryRepo_DeleteOrThrowAsync_WhenMissing_ShouldThrowNotFound()
        {
            // ---- Arrange ----
            using var db = NewContext();
            var repo = new CategoryRepo(db);

            // ---- Act / Assert ----
            await Assert.ThrowsAsync<NotFoundException>(() => repo.DeleteOrThrowAsync(999));
        }

        [Fact]
        public async Task CategoryRepo_DeleteOrThrowAsync_WhenFound_ShouldSoftDelete()
        {
            // ---- Arrange ----
            using var db = NewContext();
            var repo = new CategoryRepo(db);
            var cat = new TblCategories { Name = "AAA-ToDelete", Description = "d" };
            await repo.AddAsync(cat);
            await db.SaveChangesAsync();

            // ---- Act ----
            await repo.DeleteOrThrowAsync(cat.Id);
            await db.SaveChangesAsync();

            // ---- Assert ----
            Assert.Null(await repo.GetByIdAsync(cat.Id));
            Assert.NotNull(repo.GetAllWithDeletedAsync(
                x => x.Id == cat.Id, true).Result.First());
        }
    }
}