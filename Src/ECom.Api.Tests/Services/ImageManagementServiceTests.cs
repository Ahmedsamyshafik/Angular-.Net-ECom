using Ecom.Core.DTO.Common;
using Ecom.Core.Interfaces;
using ECom.Infrastructure.Services;
using Microsoft.AspNetCore.Hosting;
using Moq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace ECom.Api.Tests.Services
{
    public class ImageManagementServiceTests : IDisposable
    {
        private readonly string _webRoot;

        public ImageManagementServiceTests()
        {
            _webRoot = Path.Combine(Path.GetTempPath(), "ecom-imgs-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_webRoot);
        }

        public void Dispose()
        {
            if (Directory.Exists(_webRoot))
            {
                Directory.Delete(_webRoot, true);
            }
        }

        private ImageManagementService CreateService()
        {
            var environment = new Mock<IWebHostEnvironment>();
            environment.Setup(e => e.WebRootPath).Returns(_webRoot);
            return new ImageManagementService(new Mock<IPhotoRepo>().Object, environment.Object);
        }

        private static FileParameterDto Image(string fileName, byte content = 1)
        {
            return new FileParameterDto
            {
                Data = new[] { content },
                FileName = fileName,
                ContentType = "image/png"
            };
        }

        private string FullPath(string relativeSrc)
        {
            return Path.Combine(_webRoot, relativeSrc.Replace('/', Path.DirectorySeparatorChar));
        }

        private string ImageDir()
        {
            return Path.Combine(_webRoot, "imgs", "product");
        }

        private List<string> ExistingFileNames()
        {
            return Directory.Exists(ImageDir())
                ? Directory.GetFiles(ImageDir()).Select(Path.GetFileName).ToList()
                : new List<string>();
        }

        [Fact]
        public async Task AddImagesAsync_ShouldStoreFilesUnderWebRootAndReturnRelativePaths()
        {
            var service = CreateService();

            var files = new List<FileParameterDto> { Image("a.png", 1), Image("b.png", 2) };
            var paths = await service.AddImagesAsync(files, "product");

            Assert.Equal(2, paths.Count);
            Assert.All(paths, p => Assert.StartsWith("imgs/product/", p));
            Assert.All(paths, p => Assert.True(File.Exists(FullPath(p))));
        }

        [Fact]
        public async Task AddImagesAsync_WhenOneFileFails_ShouldRemoveFilesCreatedBeforeTheFailure()
        {
            var service = CreateService();

            var created = await service.AddImagesAsync(
                new List<FileParameterDto> { Image("a.png", 1), Image("b.png", 2) }, "product");

            Assert.Equal(2, created.Count);
            Assert.All(created, p => Assert.True(File.Exists(FullPath(p))));

            var before = ExistingFileNames();

            var files = new List<FileParameterDto>
            {
                Image("c.png", 3),
                Image("d.png", 4),
                Image("e." + (char)0, 5)
            };

            await Assert.ThrowsAnyAsync<Exception>(() => service.AddImagesAsync(files, "product"));

            Assert.Equal(before, ExistingFileNames());
        }

        [Fact]
        public async Task RemoveImagesAsync_ShouldDeleteOnlyTheRequestedFiles()
        {
            var service = CreateService();

            var created = await service.AddImagesAsync(
                new List<FileParameterDto> { Image("a.png", 1), Image("b.png", 2) }, "product");

            var unrelated = Path.Combine(ImageDir(), "unrelated.png");
            File.WriteAllBytes(unrelated, new byte[] { 9 });

            await service.RemoveImagesAsync(new[] { created[0] });

            Assert.False(File.Exists(FullPath(created[0])));
            Assert.True(File.Exists(FullPath(created[1])));
            Assert.True(File.Exists(unrelated));
        }

        [Fact]
        public async Task RemoveImagesAsync_WhenFileIsAlreadyMissing_ShouldNotThrow()
        {
            var service = CreateService();

            await service.RemoveImagesAsync(new[] { "imgs/product/does-not-exist.png" });

            await Task.CompletedTask;
        }
    }
}
