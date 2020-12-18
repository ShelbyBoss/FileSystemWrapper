﻿using System;
using System.IO;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Threading.Tasks;
using FileSystemCommon;
using FileSystemCommon.Models.FileSystem.Files;
using FileSystemWeb.Data;
using FileSystemWeb.Helpers;
using FileSystemWeb.Models;
using Microsoft.AspNetCore.Mvc;

namespace FileSystemWeb.Controllers
{
    [Route("api/[controller]")]
    public class FilesController : ControllerBase
    {
        private readonly AppDbContext dbContext;

        public FilesController(AppDbContext dbContext)
        {
            this.dbContext = dbContext;
        }

        [HttpGet("{encodedVirtualPath}")]
        public async Task<ActionResult> Get(string encodedVirtualPath)
        {
            string virtualPath = Utils.DecodePath(encodedVirtualPath);
            if (virtualPath == null) return BadRequest("Encoding error");
            string userId = HttpContext.User.FindFirstValue(ClaimTypes.NameIdentifier);
            InternalFile file = await ShareFileHelper.GetFileItem(virtualPath, dbContext, userId);

            if (file == null) return NotFound("Base not found");
            if (!file.Permission.Read) return Forbid();
            if (!System.IO.File.Exists(file.PhysicalPath)) return NotFound();

            return PhysicalFile(file.PhysicalPath, Utils.GetContentType(Path.GetExtension(file.PhysicalPath)), true);
        }

        [HttpGet("{encodedVirtualPath}/download")]
        public async Task<ActionResult> Download(string encodedVirtualPath)
        {
            string virtualPath = Utils.DecodePath(encodedVirtualPath);
            if (virtualPath == null) return BadRequest("Encoding error");
            string userId = HttpContext.User.FindFirstValue(ClaimTypes.NameIdentifier);
            InternalFile file = await ShareFileHelper.GetFileItem(virtualPath, dbContext, userId);

            if (file == null) return NotFound("Base not found");
            if (!file.Permission.Read) return Forbid();
            if (!System.IO.File.Exists(file.PhysicalPath)) return NotFound();

            return PhysicalFile(file.PhysicalPath,
                Utils.GetContentType(Path.GetExtension(file.PhysicalPath)),
                Path.GetFileName(file.PhysicalPath));
        }

        [HttpGet("{encodedVirtualPath}/exists")]
        public async Task<ActionResult<bool>> Exists(string encodedVirtualPath)
        {
            string virtualPath = Utils.DecodePath(encodedVirtualPath);
            if (virtualPath == null) return BadRequest("Encoding error");
            string userId = HttpContext.User.FindFirstValue(ClaimTypes.NameIdentifier);
            InternalFile file = await ShareFileHelper.GetFileItem(virtualPath, dbContext, userId);

            if (file == null) return NotFound("Base not found");
            if (!file.Permission.Info) return Forbid();

            return System.IO.File.Exists(file.PhysicalPath);
        }

        [HttpGet("{encodedVirtualPath}/info")]
        public async Task<ActionResult<FileItemInfo>> GetInfo(string encodedVirtualPath)
        {
            string virtualPath = Utils.DecodePath(encodedVirtualPath);
            if (virtualPath == null) return BadRequest("Encoding error");
            string userId = HttpContext.User.FindFirstValue(ClaimTypes.NameIdentifier);
            InternalFile file = await ShareFileHelper.GetFileItem(virtualPath, dbContext, userId);

            if (file == null) return NotFound("Base not found");
            if (!file.Permission.Info) return Forbid();

            try
            {
                FileInfo info = new FileInfo(file.PhysicalPath);
                if (!info.Exists) return NotFound();
                return FileHelper.GetInfo(file, info);
            }
            catch (FileNotFoundException)
            {
                return NotFound();
            }
        }

        [HttpGet("{encodedVirtualPath}/hash")]
        public async Task<ActionResult<string>> GetHash(string encodedVirtualPath)
        {
            string virtualPath = Utils.DecodePath(encodedVirtualPath);
            if (virtualPath == null) return BadRequest("Encoding error");
            string userId = HttpContext.User.FindFirstValue(ClaimTypes.NameIdentifier);
            InternalFile file = await ShareFileHelper.GetFileItem(virtualPath, dbContext, userId);

            if (file == null) return NotFound("Base not found");
            if (!file.Permission.Hash) return Forbid();

            try
            {
                using HashAlgorithm hasher = SHA1.Create();
                using FileStream stream = System.IO.File.OpenRead(file.PhysicalPath);
                return Convert.ToBase64String(hasher.ComputeHash(stream));
            }
            catch (FileNotFoundException)
            {
                return NotFound();
            }
        }

        [HttpPost("{encodedVirtualSrcPath}/{encodedVirtualDestPath}/copy")]
        public async Task<ActionResult> Copy(string encodedVirtualSrcPath, string encodedVirtualDestPath)
        {
            string virtualSrcPath = Utils.DecodePath(encodedVirtualSrcPath);
            if (virtualSrcPath == null) return BadRequest("Src encoding error");
            string virtualDestPath = Utils.DecodePath(encodedVirtualDestPath);
            if (virtualDestPath == null) return BadRequest("Dest encoding error");
            string userId = HttpContext.User.FindFirstValue(ClaimTypes.NameIdentifier);

            InternalFile srcFile = await ShareFileHelper.GetFileItem(virtualSrcPath, dbContext, userId);
            if (srcFile == null) return NotFound("Base of src not found");
            if (!srcFile.Permission.Read) return Forbid();

            InternalFile destFile = await ShareFileHelper.GetFileItem(virtualDestPath, dbContext, userId);
            if (destFile == null) return NotFound("Base of dest not found");
            if (!destFile.Permission.Write) return Forbid();

            try
            {
                await using Stream source = System.IO.File.OpenRead(srcFile.PhysicalPath);
                await using Stream destination = System.IO.File.Create(destFile.PhysicalPath);
                await source.CopyToAsync(destination);
            }
            catch (FileNotFoundException)
            {
                return NotFound();
            }

            return Ok();
        }

        [HttpPost("{encodedVirtualSrcPath}/{encodedVirtualDestPath}/move")]
        public async Task<ActionResult> Move(string encodedVirtualSrcPath, string encodedVirtualDestPath)
        {
            string virtualSrcPath = Utils.DecodePath(encodedVirtualSrcPath);
            if (virtualSrcPath == null) return BadRequest("Src encoding error");
            string virtualDestPath = Utils.DecodePath(encodedVirtualDestPath);
            if (virtualDestPath == null) return BadRequest("Dest encoding error");
            string userId = HttpContext.User.FindFirstValue(ClaimTypes.NameIdentifier);

            InternalFile srcFile = await ShareFileHelper.GetFileItem(virtualSrcPath, dbContext, userId);
            if (srcFile == null) return NotFound("Base of src not found");
            if (!srcFile.Permission.Write) return Forbid();

            InternalFile destFile = await ShareFileHelper.GetFileItem(virtualDestPath, dbContext, userId);
            if (destFile == null) return NotFound("Base of dest not found");
            if (!destFile.Permission.Write) return Forbid();

            try
            {
                await Task.Run(() => System.IO.File.Move(srcFile.PhysicalPath, destFile.PhysicalPath));
            }
            catch (FileNotFoundException)
            {
                return NotFound();
            }

            return Ok();
        }

        [DisableRequestSizeLimit]
        [HttpPost("{encodedVirtualPath}")]
        public async Task<ActionResult> Write(string encodedVirtualPath)
        {
            string virtualPath = Utils.DecodePath(encodedVirtualPath);
            if (virtualPath == null) return BadRequest("Encoding error");
            string userId = HttpContext.User.FindFirstValue(ClaimTypes.NameIdentifier);
            InternalFile file = await ShareFileHelper.GetFileItem(virtualPath, dbContext, userId);

            if (file == null) return NotFound("Base not found");
            if (!file.Permission.Write) return Forbid();

            string path = file.PhysicalPath;
            string tmpPath = FileHelper.GenerateUniqueFileName(path);

            try
            {
                byte[] buffer = new byte[100000];
                using (FileStream dest = System.IO.File.Create(tmpPath))
                {
                    int size;
                    Stream src = HttpContext.Request.Body;
                    do
                    {
                        size = await src.ReadAsync(buffer, 0, buffer.Length);
                        await dest.WriteAsync(buffer, 0, size);
                    } while (size > 0);
                }

                if (tmpPath != path)
                {
                    if (System.IO.File.Exists(path)) System.IO.File.Delete(path);
                    System.IO.File.Move(tmpPath, path);
                }
            }
            catch (Exception)
            {
                try
                {
                    System.IO.File.Delete(tmpPath);
                }
                catch
                {
                }

                throw;
            }

            return Ok();
        }

        [HttpDelete("{encodedVirtualPath}")]
        public async Task<ActionResult> Delete(string encodedVirtualPath)
        {
            string virtualPath = Utils.DecodePath(encodedVirtualPath);
            if (virtualPath == null) return BadRequest("Encoding error");
            string userId = HttpContext.User.FindFirstValue(ClaimTypes.NameIdentifier);
            InternalFile file = await ShareFileHelper.GetFileItem(virtualPath, dbContext, userId);

            if (file == null) return NotFound("Base not found");
            if (!file.Permission.Write) return Forbid();

            try
            {
                System.IO.File.Delete(file.PhysicalPath);
            }
            catch (FileNotFoundException)
            {
                return NotFound();
            }

            return Ok();
        }
    }
}