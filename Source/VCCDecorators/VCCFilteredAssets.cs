// Copyright (c) <2012> <Playdead>
// This file is subject to the MIT License as seen in the trunk of this repository
// Maintained by: <Kristian Kjems> <kristian.kjems+UnitySVN@gmail.com>
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace VersionControl
{
    /// <summary>
    /// Responsibility: Decorate an underlying IVersionControlCommands with filtering of assets to 
    /// remove redundant or invalid calls.
    /// Examples of things avoided : 
    /// * Revert an unversioned file 
    /// * Commit an unversioned file without adding it first
    /// * Unlock a file that is not locked
    /// </summary>
    public class VCCFilteredAssets : VCCDecorator
    {
        public VCCFilteredAssets(IVersionControlCommands vcc)
            : base(vcc)
        {
        }

        public override bool Status(bool remote, bool full)
        {
            return base.Status(remote, full);
        }

        public override VersionControlStatus GetAssetStatus(string assetPath)
        {
            if (InUnversionedParentFolder(assetPath)) return new VersionControlStatus() { assetPath = assetPath, fileStatus = VCFileStatus.Unversioned };
            return vcc.GetAssetStatus(assetPath);
        }

        public override bool Status(IEnumerable<string> assets, bool remote)
        {
            assets = NonPending(InVersionedFolder(NonEmpty(assets)));
            return assets.Any() ? base.Status(assets, remote) : false;
        }

        public override bool Update(IEnumerable<string> assets = null, bool force = true)
        {
            return base.Update((assets != null ? Versioned(assets) : null), force);
        }

        public override bool Commit(IEnumerable<string> assets, string commitMessage = "")
        {
            var filesInFolders = AddFilesInFolders(assets, true);
            var toBeCommited = filesInFolders.Where(a => vcc.GetAssetStatus(a).fileStatus != VCFileStatus.Normal || Directory.Exists(a));
            return
                base.Add(UnversionedInVersionedFolder(filesInFolders)) &&
                base.Delete(Missing(filesInFolders)) &&
                base.Commit(ShortestFirst(toBeCommited), commitMessage) &&
                base.Status(false, false) &&
                ReleaseLock(assets);
        }

        public override bool Add(IEnumerable<string> assets)
        {
            return base.Add(UnversionedInVersionedFolder(assets));
        }

        public override bool Revert(IEnumerable<string> assets)
        {
            return base.Revert(NonNormal(assets));
        }

        public override bool Delete(IEnumerable<string> assets, bool force = false)
        {
            return base.Delete(Versioned(assets), force);
        }

        public override bool GetLock(IEnumerable<string> assets, bool force = false)
        {
            return base.GetLock(force ? Versioned(assets) : NotLocked(assets), force);
        }

        public override bool ReleaseLock(IEnumerable<string> assets)
        {
            return base.ReleaseLock(Locked(assets));
        }

        public override bool ChangeListAdd(IEnumerable<string> assets, string changelist)
        {
            return base.ChangeListAdd(Versioned(assets), changelist);
        }

        public override bool ChangeListRemove(IEnumerable<string> assets)
        {
            return base.ChangeListRemove(Versioned(OnChangeList(assets)));
        }

        public override bool Move(string from, string to)
        {
            if (vcc.GetAssetStatus(from).fileStatus == VCFileStatus.Unversioned) return false;
            if (InUnversionedParentFolder(to)) return false;
            return base.Move(from, to);
        }

        #region Filters

        IEnumerable<string> NonPending(IEnumerable<string> assets)
        {
            return assets.Where(a => vcc.GetAssetStatus(a).reflectionLevel != VCReflectionLevel.Pending);
        }
        IEnumerable<string> NonEmpty(IEnumerable<string> assets)
        {
            return assets.Where(a => !string.IsNullOrEmpty(a));
        }
        IEnumerable<string> Unversioned(IEnumerable<string> assets)
        {
            return assets.Where(a => vcc.GetAssetStatus(a).fileStatus == VCFileStatus.Unversioned || InUnversionedParentFolder(a));
        }
        IEnumerable<string> InVersionedFolder(IEnumerable<string> assets)
        {
            return assets.Where(a => !InUnversionedParentFolder(a));
        }
        IEnumerable<string> UnversionedInVersionedFolder(IEnumerable<string> assets)
        {
            return assets.Where(a => vcc.GetAssetStatus(a).fileStatus == VCFileStatus.Unversioned && !InUnversionedParentFolder(a));
        }
        IEnumerable<string> Versioned(IEnumerable<string> assets)
        {
            return assets.Where(a => vcc.GetAssetStatus(a).fileStatus != VCFileStatus.Unversioned && !InUnversionedParentFolder(a));
        }
        IEnumerable<string> OnChangeList(IEnumerable<string> assets)
        {
            return assets.Where(a => !string.IsNullOrEmpty(vcc.GetAssetStatus(a).changelist));
        }
        IEnumerable<string> Missing(IEnumerable<string> assets)
        {
            return assets.Where(a => vcc.GetAssetStatus(a).fileStatus == VCFileStatus.Missing);
        }
        IEnumerable<string> NonNormal(IEnumerable<string> assets)
        {
            return assets.Where(a => vcc.GetAssetStatus(a).fileStatus != VCFileStatus.Normal);
        }
        IEnumerable<string> Normal(IEnumerable<string> assets)
        {
            return assets.Where(a => vcc.GetAssetStatus(a).fileStatus == VCFileStatus.Normal);
        }
        IEnumerable<string> Modified(IEnumerable<string> assets)
        {
            return assets.Where(a => vcc.GetAssetStatus(a).fileStatus == VCFileStatus.Modified);
        }
        IEnumerable<string> Deleted(IEnumerable<string> assets)
        {
            return assets.Where(a => vcc.GetAssetStatus(a).fileStatus == VCFileStatus.Deleted);
        }
        IEnumerable<string> Conflicted(IEnumerable<string> assets)
        {
            return assets.Where(a => vcc.GetAssetStatus(a).fileStatus == VCFileStatus.Conflicted);
        }
        IEnumerable<string> Locked(IEnumerable<string> assets)
        {
            return assets.Where(a => vcc.GetAssetStatus(a).lockStatus == VCLockStatus.LockedHere);
        }
        IEnumerable<string> NotLocked(IEnumerable<string> assets)
        {
            return
                assets.Where(a => vcc.GetAssetStatus(a).fileStatus != VCFileStatus.Unversioned &&
                vcc.GetAssetStatus(a).lockStatus == VCLockStatus.NoLock);
        }
        private static IEnumerable<string> ShortestFirst(IEnumerable<string> assets)
        {
            return assets.OrderBy(s => s.Length);
        }
        private IEnumerable<string> AddFolders(IEnumerable<string> assets)
        {
            return assets
                .Select(a => Path.GetDirectoryName(a))
                .Where(d => GetAssetStatus(d).fileStatus != VCFileStatus.Normal)
                .Concat(assets)
                .Distinct();
        }

        private bool InUnversionedParentFolder(string asset)
        {
            return ParentFolders(asset).Any(a => vcc.GetAssetStatus(a).fileStatus == VCFileStatus.Unversioned);
        }

        private static IEnumerable<string> ParentFolders(string asset)
        {
            const char pathSeparator = '/';
            var parentFolders = new List<string>();
            if (!string.IsNullOrEmpty(asset))
            {
                string currentFolder = "";
                foreach (var folderIt in Path.GetDirectoryName(asset).Split(pathSeparator))
                {
                    currentFolder += folderIt + pathSeparator;
                    parentFolders.Add(currentFolder.TrimEnd(pathSeparator));
                }
            }
            return parentFolders;
        }

        private IEnumerable<string> AddFilesInFolders(IEnumerable<string> assets, bool versionedFoldersOnly = false)
        {
            foreach (var assetIt in new List<string>(assets))
            {
                if (Directory.Exists(assetIt) && (!versionedFoldersOnly || GetAssetStatus(assetIt).fileStatus != VCFileStatus.Unversioned))
                {
                    assets = assets
                        .Concat(Directory.GetFiles(assetIt, "*", SearchOption.AllDirectories)
                        .Where(a => (File.GetAttributes(a) & FileAttributes.Hidden) == 0)
                        .Select(s => s.Replace("\\", "/")));
                }
            }
            return assets;
        }

        private IEnumerable<string> RemoveFolders(IEnumerable<string> assets)
        {
            return assets.Where(a => !Directory.Exists(a));
        }

        private IEnumerable<string> RemoveFilesUnderUnversionedFolders(IEnumerable<string> assets)
        {
            var folders = assets.Where(a => Directory.Exists(a) && GetAssetStatus(a).fileStatus == VCFileStatus.Unversioned);
            assets = assets.Where(a => !folders.Any(f => a.StartsWith(f) && a != f));
            return assets;
        }
        #endregion
    }

}
