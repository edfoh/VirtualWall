using System;
using Android.App;
using Android.Content;
using Android.Runtime;
using Android.Views;
using Android.Widget;
using Android.OS;

namespace VirtualWall
{
	[Activity (Label = "Virtual Wall")]
	public class MainActivity : Activity
	{
		protected override void OnCreate (Bundle bundle)
		{
			base.OnCreate (bundle);

			// Set our view from the "main" layout resource
			SetContentView (Resource.Layout.Main);

			// Get our button from the layout resource,
			// and attach an event to it
			Button button = FindViewById<Button> (Resource.Id.write_nfc_button);
			
			button.Click += WriteNfc_Click;
		}

		private void WriteNfc_Click (object sender, EventArgs e)
		{
			Intent intent = new Intent (this, typeof(WriteNfcActivity));
			EditText editText = FindViewById<EditText>(Resource.Id.write_text);
			String message = editText.Text;
			intent.PutExtra (StringConstants.WriteNfMessageKey, message);
			StartActivity(intent);
		}
	}
}


