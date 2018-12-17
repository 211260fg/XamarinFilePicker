using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Android.Content;
using Android.Content.Res;
using Android.Graphics;
using Android.OS;
using Android.Support.V4.Content;
using Android.Support.V7.App;
using Android.Support.V7.Widget;
using Android.Util;
using Android.Views;
using Android.Widget;
using Java.IO;
using Java.Net;
using Orientation = Android.Widget.Orientation;

namespace FolderPicker
{
    public enum FileSelectionType
    {
        File, Directory
    }

    public interface IFileSelectionListener
    {
        void OnFileSelected(FileInfo file);
        void OnDirectorySelected(FileInfo file);
    }

    public class FilePickerDialog : AlertDialog.Builder, IFileSelectionListener, IDialogInterfaceOnKeyListener
    {
        private LinearLayout _backStackLayout;
        private FilesAdapter _adapter;

        private WeakReference<IFileSelectionListener> _fileSelectionListener;
        private FileSelectionType _selectionType = FileSelectionType.File;
        private bool _showHiddenFiles = true;

        private readonly List<FileInfo> _backStack = new List<FileInfo>();

        public FileInfo SelectedDirectory { get; private set; }

        public FilePickerDialog(Context context) : base(context)
        {
            Initialize();
        }

        public FilePickerDialog(Context context, int themeResId) : base(context, themeResId)
        {
            Initialize();
        }

        private void Initialize()
        {
            //TODO move to a layout
            var layout = new LinearLayout(Context)
            {
                Orientation = Orientation.Vertical,
                LayoutParameters = new ViewGroup.LayoutParams(ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.WrapContent)
            };

            var backStackScroller = new ScrollView(Context) { LayoutParameters = new ViewGroup.LayoutParams(ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.WrapContent) };
            _backStackLayout = new LinearLayout(Context)
            {
                Orientation = Orientation.Horizontal,
                LayoutParameters = new ViewGroup.LayoutParams(ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.WrapContent)
            };
            backStackScroller.AddView(_backStackLayout);
            layout.AddView(backStackScroller);

            var filesRv = new RecyclerView(Context);
            filesRv.SetLayoutManager(new LinearLayoutManager(Context));
            filesRv.SetAdapter(_adapter = new FilesAdapter() { FileSelectionListener = new WeakReference<IFileSelectionListener>(this) });
            layout.AddView(filesRv);

            OnDirectorySelected(null);

            SetOnKeyListener(this);

            SetView(layout);
        }

        public FilePickerDialog ShowHiddenFiles(bool showHiddenFiles)
        {
            _showHiddenFiles = showHiddenFiles;
            if (SelectedDirectory != null)
                OnDirectorySelected(SelectedDirectory);
            return this;
        }

        public FilePickerDialog OnFileSelected(IFileSelectionListener listener)
        {
            _fileSelectionListener = new WeakReference<IFileSelectionListener>(listener);
            return this;
        }
        
        public FilePickerDialog SelectionType(FileSelectionType type)
        {
            _selectionType = type;
            return this;
        }

        private static List<FileInfo> GetAllStorages(Context context)
        {
            var storages = new List<FileInfo>();
            try
            {
                var externalStorageFiles = ContextCompat.GetExternalFilesDirs(context, null);
                var basePath = $"/Android/data/{context.PackageName}/files";

                foreach (var file in externalStorageFiles)
                {
                    if (file != null)
                    {
                        var path = file.AbsolutePath;
                        if (path.Contains(basePath))
                        {
                            var finalPath = path.Replace(basePath, "");
                            if (IsValidPath(finalPath))
                            {
                                storages.Add(new FileInfo(new File(finalPath)));
                            }
                        }
                    }
                }
            }
            catch (Exception e)
            {
                //TODO log exception
                Toast.MakeText(context, "Failed to access storage", ToastLength.Short).Show();
            }

            return storages;
        }

        private static bool IsValidPath(string path)
        {
            try
            {
                var stat = new StatFs(path);
                var blocks = stat.BlockCountLong;
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public void OnFileSelected(FileInfo file)
        {
            _fileSelectionListener?.TryGetTarget()?.OnFileSelected(file);
        }

        public async void OnDirectorySelected(FileInfo file)
        {
            SelectedDirectory = file;
            if (file != null)
            {
                IEnumerable<File> children = await file.GetChildren();
                if (_selectionType == FileSelectionType.Directory)
                    children = children.Where(f => f.IsDirectory);
                if (!_showHiddenFiles)
                    children = children.Where(f => f.IsHidden == false);
                _adapter.Files = children?.Select(c => new FileInfo(c)).ToList();
                if (!_backStack.Contains(file))
                    _backStack.Add(file);
                else
                {
                    //remove everything after the file in the backstack
                    var index = _backStack.IndexOf(file)+1;
                    _backStack.RemoveRange(index, _backStack.Count - index);
                }

                _fileSelectionListener?.TryGetTarget()?.OnDirectorySelected(file);
            }
            else
            {
                _adapter.Files = GetAllStorages(Context);
            }

            UpdateBackStackView();

            _adapter.NotifyDataSetChanged();
        }

        private void UpdateBackStackView()
        {
            _backStackLayout.RemoveAllViews();

            for (var i = 0; i < _backStack.Count; i++)
            {
                var file = _backStack[i];
                if (i > 0)
                    _backStackLayout.AddView(new TextView(Context) { Text = " > " });

                var folderLabel = new FolderLabelView(Context)
                {
                    File = file,
                    Listener = new WeakReference<IFileSelectionListener>(this),
                    Typeface = i == _backStack.Count - 1 ? Typeface.DefaultBold : Typeface.Default
                };
                
                _backStackLayout.AddView(folderLabel);
            }
        }

        public bool OnKey(IDialogInterface dialog, Keycode keyCode, KeyEvent e)
        {
            if (e.Action != KeyEventActions.Down && keyCode == Keycode.Back)
            {
                if (GoBack())
                    return false;

                dialog.Dismiss();
            }

            return true;
        }

        public bool GoBack()
        {
            if (_backStack.Any())
            {
                _backStack.RemoveAt(_backStack.Count - 1);
                OnDirectorySelected(_backStack.LastOrDefault());
                return true;
            }

            return false;
        }
    }

    internal class FilesAdapter : RecyclerView.Adapter
    {
        public List<FileInfo> Files { get; set; }

        public bool ShowHiddenFiles { get; set; } = true;

        public WeakReference<IFileSelectionListener> FileSelectionListener { get; set; }

        public override void OnBindViewHolder(RecyclerView.ViewHolder holder, int position)
        {
            var vh = (FileViewHolder)holder;
            var fileInfo = Files[position];
            vh.Load(fileInfo);
        }

        public override RecyclerView.ViewHolder OnCreateViewHolder(ViewGroup parent, int viewType)
        {
            return new FileViewHolder(parent) { FileSelectionListener = FileSelectionListener };
        }

        public override int ItemCount => Files?.Count ?? 0;
    }

    internal class FileViewHolder : RecyclerView.ViewHolder, View.IOnClickListener
    {
        private readonly ImageView _iconView;
        private readonly TextView _extensionView;
        private readonly TextView _labelView;
        private readonly TextView _sizeView;

        private FileInfo _fileInfo;

        public WeakReference<IFileSelectionListener> FileSelectionListener { get; set; }

        public FileViewHolder(ViewGroup parent) : base(LayoutInflater.From(parent.Context).Inflate(Resource.Layout.FileView, parent, false))
        {
            _iconView = ItemView.FindViewById<ImageView>(Resource.Id.icon);
            _extensionView = ItemView.FindViewById<TextView>(Resource.Id.extension);
            _labelView = ItemView.FindViewById<TextView>(Resource.Id.name);
            _sizeView = ItemView.FindViewById<TextView>(Resource.Id.size);

            ItemView.SetOnClickListener(this);
        }

        public async void Load(FileInfo fileInfo)
        {
            _fileInfo = fileInfo;
            _iconView.SetImageResource(GetIconResource(fileInfo));
            _labelView.Text = fileInfo.Label;
            _extensionView.Text = fileInfo.FileType == FileType.Unkown ? fileInfo.Extension : null;
            _extensionView.Visibility = string.IsNullOrEmpty(_extensionView.Text) ? ViewStates.Invisible : ViewStates.Visible;
            if (fileInfo.IsDirectory)
            {
                var children = await fileInfo.GetChildren();
                _sizeView.Text = $"{children?.Length} items";
            }
            else
            {
                _sizeView.Text = fileInfo.Size;
            }
        }

        private int GetIconResource(FileInfo file)
        {
            switch (file.FileType)
            {
                case FileType.DeviceStorage:
                    if (ItemView.Resources.GetBoolean(Resource.Boolean.IsTablet))
                        return Resource.Drawable.ic_device_tablet;
                    return Resource.Drawable.ic_device_phone;
                case FileType.SdStorage:
                    return Resource.Drawable.ic_sd;
                case FileType.Directory:
                    return Resource.Drawable.ic_folder;
                case FileType.Image:
                    return Resource.Drawable.ic_image;
                case FileType.Audio:
                    return Resource.Drawable.ic_audio;
                case FileType.Video:
                    return Resource.Drawable.ic_video;
                case FileType.Pdf:
                    return Resource.Drawable.ic_pdf;
                case FileType.Unkown:
                    return Resource.Drawable.ic_file;
                default:
                    return Resource.Drawable.ic_file;
            }
        }

        public void OnClick(View v)
        {
            var listener = FileSelectionListener?.TryGetTarget();
            if (_fileInfo.IsDirectory)
                listener?.OnDirectorySelected(_fileInfo);
            else
                listener?.OnFileSelected(_fileInfo);
        }
    }

    internal class FolderLabelView : TextView, View.IOnClickListener
    {
        private int Padding => (int)SystemHelper.ConvertDpToPixel(Resources, 8);

        private FileInfo _file;

        public FileInfo File
        {
            get => _file;
            set
            {
                _file = value;
                LoadView();
            }
        }

        public WeakReference<IFileSelectionListener> Listener { get; set; }

        public FolderLabelView(Context context) : base(context)
        {
            Initialize();
        }

        private void Initialize()
        {
            var attrs = new[] { Android.Resource.Attribute.SelectableItemBackground };
            var typedArray = Context.ObtainStyledAttributes(attrs);
            var backgroundResource = typedArray.GetResourceId(0, 0);
            SetBackgroundResource(backgroundResource);
            typedArray.Recycle();
            SetOnClickListener(this);
            SetPadding(Padding, Padding, Padding, Padding);
            SetMaxLines(1);
        }

        private void LoadView()
        {
            Text = File.Label;
        }

        public void OnClick(View v)
        {
            Listener?.TryGetTarget()?.OnDirectorySelected(File);
        }
    }

    public class FileInfo
    {
        private string _label;
        private string _absolutePath;
        private string _mimeType;
        private string _extension;
        private bool? _isDirectory;
        private FileType? _fileType;

        public FileInfo(File file)
        {
            File = file;
        }

        public File File { get; }

        public string Path => _absolutePath ?? (_absolutePath = File.AbsolutePath);
        public bool IsDirectory => _isDirectory ?? (_isDirectory = File.IsDirectory).Value;
        public string Extension => _extension ?? (_extension = GetExtension());

        public string Size => SpaceFormatter.Format(File.Length());

        public string Label
        {
            get
            {
                if (_label != null)
                    return _label;

                if (IsDeviceStorage())
                    _label = "Device storage";

                else if (IsExternalSdStorage())
                    _label = "External storage";

                else
                    _label = File.Name;

                return _label;
            }
        }

        public string MimeType
        {
            get
            {
                if (_mimeType != null)
                    return _mimeType;

                try
                {
                    _mimeType = URLConnection.GuessContentTypeFromName(Path);
                }
                catch (Exception)
                {
                    _mimeType = "*/*";
                }

                return _mimeType;
            }
        }

        public FileType FileType
        {
            get
            {
                if (_fileType.HasValue)
                    return _fileType.Value;

                if (IsDeviceStorage())
                    _fileType = FileType.DeviceStorage;

                else if (IsExternalSdStorage())
                    _fileType = FileType.SdStorage;

                else if (IsDirectory)
                    _fileType = FileType.Directory;

                else if (MimeType?.StartsWith("image/") == true)
                    _fileType = FileType.Image;

                else if (MimeType?.StartsWith("audio/") == true)
                    _fileType = FileType.Audio;

                else if (MimeType?.StartsWith("video/") == true)
                    _fileType = FileType.Video;

                else if (MimeType?.StartsWith("application/pdf") == true)
                    _fileType = FileType.Pdf;

                else
                    _fileType = FileType.Unkown;

                return _fileType.Value;
            }
        }


        private bool IsDeviceStorage()
        {
            return Path.Equals("/storage/emulated/0");
        }

        private bool IsExternalSdStorage()
        {
            //TODO the list of external storages should be checked instead of this
            var regex = new Regex("(\\/)(storage|mnt)(\\/)([^\\/]*)$", RegexOptions.IgnoreCase);
            var m = regex.Match(Path);
            return m.Success;
        }

        public async Task<File[]> GetChildren()
        {
            return await File.ListFilesAsync();
        }

        private string GetExtension()
        {
            var ext = System.IO.Path.GetExtension(Path);

            if (ext != null && ext.Length > 3)
                ext = ext.Substring(0, 4);

            return ext;
        }
    }

    public enum FileType
    {
        DeviceStorage, SdStorage, Directory, Image, Audio, Video, Pdf, Unkown
    }

    internal class SpaceFormatter
    {
        public static string Format(long originalSize)
        {
            var label = "B";
            double size = originalSize;

            if (size > 1024)
            {
                size /= 1024;
                label = "KB";
            }

            if (size > 1024)
            {
                size /= 1024;
                label = "MB";
            }

            if (size > 1024)
            {
                size /= 1024;
                label = "GB";
            }

            if (size % 1 == 0)
            {
                return $"{size} {label}";
            }

            return $"{size:F2} {label}";
        }
    }

    internal static class SystemHelper
    {
        public static float ConvertDpToPixel(Resources resources, int dp)
        {
            return TypedValue.ApplyDimension(ComplexUnitType.Dip, dp, resources.DisplayMetrics);
        }
    }

    internal static class WeakReferenceExtensions
    {
        public static T TryGetTarget<T>(this WeakReference<T> weakReference) where T : class
        {
            T result = null;
            weakReference?.TryGetTarget(out result);
            return result;
        }
    }
}