
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Android.App;
using Android.Content;
using Android.OS;
using Android.Runtime;
using Android.Views;
using Android.Widget;
using Android.Nfc;
using Android.Nfc.Tech;
using Java.IO;
using Android.Util;
using System.Threading.Tasks;
using Java.Lang;

namespace VirtualWall
{
	[Activity (ParentActivity=typeof(MainActivity), Label = "Read NFC"),
		IntentFilter(new[] { "android.nfc.action.NDEF_DISCOVERED", "android.nfc.action.TECH_DISCOVERED" },
			DataMimeType = "text/plain",
			Categories = new[] { "android.intent.category.DEFAULT" }),
		MetaData("android.nfc.action.TECH_DISCOVERED", Value="@xml/nfc_tech_list")]			
	public class ReadNfcActivity : Activity
	{
		private NfcAdapter nfcAdapter;
		private TextView textView;
		private ProgressDialog progressDialog;

		protected override void OnCreate (Bundle bundle)
		{
			base.OnCreate (bundle);
			SetContentView (Resource.Layout.ReadNfc);

			textView = FindViewById<TextView>(Resource.Id.textView_explanation);

			nfcAdapter = NfcAdapter.GetDefaultAdapter(this);

			if (nfcAdapter == null) {
				// Stop here, we definitely need NFC
				Toast.MakeText(this, "This device doesn't support NFC.", ToastLength.Long).Show();
				this.Finish();
				return;

			}

			if (!nfcAdapter.IsEnabled) {
				textView.Text = "NFC is disabled.";
			} else {
				textView.Text = "Please scan an NFC";
			}

			progressDialog = new ProgressDialog(this) { Indeterminate = true };
			progressDialog.SetTitle("Read In Progress");
			progressDialog.SetMessage("Please wait...");

			HandleIntent(this.Intent);
		}

		protected override void OnResume ()
		{
			base.OnResume ();
			SetupForegroundDispatch (this, nfcAdapter);
		}

		protected override void OnPause ()
		{
			StopForegroundDispatch (this, nfcAdapter);
			base.OnPause ();
		}

		protected override void OnNewIntent (Intent intent)
		{
			HandleIntent(intent);
		}

		private void HandleIntent(Intent intent) {
			string action = intent.Action;
			if (NfcAdapter.ActionNdefDiscovered.Equals(action))
			{
				string type = intent.Type;
				if (StringConstants.MimeType.Equals(type)) {

					Tag tag = (Tag)intent.GetParcelableExtra(NfcAdapter.ExtraTag);
					ReadAsync(tag);
				} 
				else 
				{
					Log.Debug("VirtualWall", "Wrong mime type: " + type);
				}
			} else if (NfcAdapter.ActionTechDiscovered.Equals(action)) {

				// In case we would still use the Tech Discovered Intent
				Tag tag = (Tag)intent.GetParcelableExtra(NfcAdapter.ExtraTag);
				string[] techList = tag.GetTechList();
				string searchedTech = typeof(Ndef).Name;

				foreach (string tech in techList) {
					if (searchedTech.Equals(tech)) 
					{
						ReadAsync(tag);
						break;
					}
				}
			}
		}

		private void ReadAsync(Tag tag)
		{
			Task.Factory
		        .StartNew(() =>
					ReadTagAsync(tag)
			        )
		        .ContinueWith(task =>
			            RunOnUiThread(() =>
								OnSuccessfulRead(task.Result)
				            )
			        );
		}

		private string ReadTagAsync(Tag tag)
		{
			Ndef ndef = Ndef.Get(tag);
			if (ndef == null) {
				// NDEF is not supported by this Tag.
				return null;
			}

			NdefMessage ndefMessage = ndef.CachedNdefMessage;

			NdefRecord[] records = ndefMessage.GetRecords();
			foreach (NdefRecord ndefRecord in records) 
			{
				if (ndefRecord.Tnf == NdefRecord.TnfWellKnown) {
					try {
						return ReadText(ndefRecord);
					} catch (UnsupportedEncodingException ex) {
						Log.Error("VirtualWall", "Unsupported Encoding", ex);
					}
				}
			}

			return null;
		}

		private string ReadText(NdefRecord record) {
			return Encoding.UTF8.GetString(record.GetPayload());
		}

		private void OnSuccessfulRead(string nfcData)
		{
	        progressDialog.Hide();
			textView.Text = nfcData;
	    }

		public static void SetupForegroundDispatch(Activity activity, NfcAdapter adapter) 
		{
			Intent intent = new Intent(activity.ApplicationContext, activity.GetType());
			intent.SetFlags(ActivityFlags.SingleTop);

			PendingIntent pendingIntent = PendingIntent.GetActivity(activity.ApplicationContext, 0, intent, 0);

			IntentFilter[] filters = new IntentFilter[1];
			string[][] techList = new string[][]{};

			// Notice that this is the same filter as in our manifest.
			filters[0] = new IntentFilter();
			filters[0].AddAction(NfcAdapter.ActionNdefDiscovered);
			filters[0].AddCategory(Intent.CategoryDefault);
			try {
				filters[0].AddDataType(StringConstants.MimeType);
			} catch (Android.Content.IntentFilter.MalformedMimeTypeException) {
				throw new RuntimeException("Check your mime type.");
			}

			adapter.EnableForegroundDispatch(activity, pendingIntent, filters, techList);
		}
			
		public static void StopForegroundDispatch(Activity activity, NfcAdapter adapter)
		{
			adapter.DisableForegroundDispatch(activity);
		}
	}
}

