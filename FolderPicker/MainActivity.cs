using System;
using System.Collections.Generic;
using System.Linq;
using Android;
using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using Android.Support.V4.App;
using Android.Support.V4.Content;
using Android.Support.V7.App;
using Android.Views;
using Android.Widget;
using Java.IO;
using AlertDialog = Android.App.AlertDialog;

namespace FolderPicker
{
    [Activity(Label = "@string/app_name", Theme = "@style/AppTheme", MainLauncher = true)]
    public class MainActivity : AppCompatActivity, View.IOnClickListener
    {
        protected override void OnCreate(Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState);
            // Set our view from the "main" layout resource
            SetContentView(Resource.Layout.activity_main);

            var folderBtn = FindViewById<Button>(Resource.Id.btn_folder);
            folderBtn.SetOnClickListener(this);

        }

        public override void OnRequestPermissionsResult(int requestCode, string[] permissions, Permission[] grantResults)
        {
            base.OnRequestPermissionsResult(requestCode, permissions, grantResults);
            if (requestCode == 0)
            {
                if (grantResults.Length > 0 && grantResults[0] == Permission.Granted)
                    SelectFile();
            }
        }

        public void OnClick(View v)
        {
            SelectFile();
        }

        private async void SelectFile()
        {
            if (ContextCompat.CheckSelfPermission(this, Manifest.Permission.WriteExternalStorage) != Permission.Granted)
            {
                ActivityCompat.RequestPermissions(this, new[] { Manifest.Permission.WriteExternalStorage }, 0);
                return;
            }
            
            var dialog = new FilePickerDialog(this);
            
            dialog
                .SelectionType(FileSelectionType.Directory)
                .ShowHiddenFiles(false)
                .SetTitle("Select a folder")
                .SetNegativeButton("Cancel", listener: null)
                .SetPositiveButton("OK", (sender, args) => { Toast.MakeText(this, dialog.SelectedDirectory?.Label, ToastLength.Short).Show(); })
                .Show();
        }
    }
}