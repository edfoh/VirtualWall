
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
using System.IO;

namespace VirtualWall
{
	[Activity (ParentActivity=typeof(MainActivity), Label = "Write NFC")]			
	public class WriteNfcActivity : Activity
	{
		private NfcAdapter nfcAdapter;  
		private IntentFilter[] writeTagFilters;  
		private PendingIntent nfcPendingIntent;   
		private bool writeProtect = false;  
		private Context context;  

		private String textToWrite;

		protected override void OnCreate (Bundle bundle)
		{
			base.OnCreate (bundle);
			SetContentView(Resource.Layout.WriteNfc);
		
			textToWrite = Intent.GetStringExtra(StringConstants.WriteNfMessageKey);

			context = Application.Context;  
			nfcAdapter = NfcAdapter.GetDefaultAdapter(this);  
			nfcPendingIntent = PendingIntent.GetActivity(this, 0, new Intent(this,  
				typeof(WriteNfcActivity)).AddFlags(ActivityFlags.SingleTop   
					| ActivityFlags.ClearTop), 0);  
			IntentFilter discovery = new IntentFilter(NfcAdapter.ActionTagDiscovered);  
			IntentFilter ndefDetected = new IntentFilter(NfcAdapter.ActionNdefDiscovered);      
			IntentFilter techDetected = new IntentFilter(NfcAdapter.ActionTechDiscovered);  
			// Intent filters for writing to a tag  
			writeTagFilters = new IntentFilter[] { discovery }; 
		}

		protected override void OnResume ()
		{
			base.OnResume ();
			if(nfcAdapter != null) {  
				if (!nfcAdapter.IsEnabled){  
					Toast.MakeText(context, "NFC not enabled. Please enable it in Settings", ToastLength.Short).Show();  
				} 
				else
				{
					nfcAdapter.EnableForegroundDispatch(this, nfcPendingIntent, writeTagFilters, null);  
				}
			} else {  
				Toast.MakeText(context, "Sorry, No NFC Adapter found.", ToastLength.Short).Show();  
			}  
		}

		protected override void OnPause ()
		{
			base.OnPause ();
			if (nfcAdapter != null) {
				nfcAdapter.DisableForegroundDispatch (this); 
			}
		}

		protected override void OnNewIntent (Intent intent)
		{
			base.OnNewIntent (intent);
			if(NfcAdapter.ActionTagDiscovered.Equals(intent.Action)) {
				// validate that this tag can be written....
				Tag detectedTag = (Tag)intent.GetParcelableExtra(NfcAdapter.ExtraTag);
				if(SupportedTechs(detectedTag.GetTechList())) {
					// check if tag is writable (to the extent that we can
					if(WritableTag(detectedTag)) {
						//writeTag here
						WriteResponse wr = WriteTag(GetTagAsNdef(textToWrite), detectedTag);
						String message = (wr.Status == 1? "Success: " : "Failed: ") + wr.Message;
						Toast.MakeText(context,message,ToastLength.Short).Show();
						Intent mainActivityIntent = new Intent(this, typeof(MainActivity));
						StartActivity(mainActivityIntent);

					} else {
						Toast.MakeText(context,"This tag is not writable",ToastLength.Short).Show();

					}	            
				} else {
					Toast.MakeText(context,"This tag type is not supported",ToastLength.Short).Show();
				}
			}
		}

		private bool SupportedTechs(String[] techs) 
		{
			bool ultralight = false;
			bool nfcA = false;
			bool ndef = false;
			foreach(var tech in techs) 
			{
				if(tech.Equals("android.nfc.tech.MifareUltralight")) {
					ultralight=true;
				}else if(tech.Equals("android.nfc.tech.NfcA")) { 
					nfcA=true;
				} else if(tech.Equals("android.nfc.tech.Ndef") || tech.Equals("android.nfc.tech.NdefFormatable")) {
					ndef=true;

				}
			}
			if(ultralight && nfcA && ndef) {
				return true;
			} else {
				return false;
			}
		}

		private bool WritableTag(Tag tag) 
		{
			try {
				Ndef ndef = Ndef.Get(tag);
				if (ndef != null) 
				{
					ndef.Connect();
					if (!ndef.IsWritable) {
						Toast.MakeText(context,"Tag is read-only.",ToastLength.Short).Show();
						ndef.Close(); 
						return false;
					}
					ndef.Close();
					return true;
				} 
			} catch (Exception) {
				Toast.MakeText(context,"Failed to read tag",ToastLength.Short).Show();
			}

			return false;
		}

		private NdefRecord CreateTextRecord(string payload) 
		{
			byte[] textBytes = Encoding.UTF8.GetBytes(payload);
			NdefRecord record = new NdefRecord(NdefRecord.TnfWellKnown,
				NdefRecord.RtdText.ToArray(), new byte[0], textBytes);
			return record;
		}

		private NdefMessage GetTagAsNdef(String payload) 
		{
			NdefRecord record = CreateTextRecord (payload);
			return new NdefMessage(
				new NdefRecord[] {record});
		}

		public WriteResponse WriteTag(NdefMessage message, Tag tag) 
		{
			int size = message.ToByteArray().Length;
			string mess = "";

			try {
				Ndef ndef = Ndef.Get(tag);
				if (ndef != null) {
					ndef.Connect();

					if (!ndef.IsWritable) {
						return new WriteResponse(0,"Tag is read-only");

					}
					if (ndef.MaxSize < size) {
						mess = "Tag capacity is " + ndef.MaxSize + " bytes, message is " + size
							+ " bytes.";
						return new WriteResponse(0,mess);
					}

					ndef.WriteNdefMessage(message);
					if(writeProtect)  ndef.MakeReadOnly();
					mess = "Wrote message to pre-formatted tag.";
					return new WriteResponse(1,mess);
				} else {
					NdefFormatable format = NdefFormatable.Get(tag);
					if (format != null) {
						try {
							format.Connect();
							format.Format(message);
							mess = "Formatted tag and wrote message";
							return new WriteResponse(1,mess);
						} catch (IOException) {
							mess = "Failed to format tag.";
							return new WriteResponse(0,mess);
						}
					} else {
						mess = "Tag doesn't support NDEF.";
						return new WriteResponse(0,mess);
					}
				}
			} catch (Exception) {
				mess = "Failed to write tag";
				return new WriteResponse(0,mess);
			}
		}

		public class WriteResponse {

			public WriteResponse(int status, string message) {
				Status = status;
				Message = message;
			}

			public int Status { get; private set; }
			public string Message { get; private set; }
		}
	}
}

