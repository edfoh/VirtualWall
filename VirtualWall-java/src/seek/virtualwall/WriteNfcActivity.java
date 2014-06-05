package seek.virtualwall;

import java.io.IOException;
import java.nio.charset.Charset;
import java.util.Locale;

import android.app.Activity;
import android.app.ActionBar;
import android.app.AlertDialog;
import android.app.Fragment;
import android.app.PendingIntent;
import android.content.Context;
import android.content.DialogInterface;
import android.content.Intent;
import android.content.IntentFilter;
import android.media.SoundPool;
import android.nfc.NdefMessage;
import android.nfc.NdefRecord;
import android.nfc.NfcAdapter;
import android.nfc.Tag;
import android.nfc.tech.Ndef;
import android.nfc.tech.NdefFormatable;
import android.os.Bundle;
import android.view.LayoutInflater;
import android.view.Menu;
import android.view.MenuItem;
import android.view.View;
import android.view.ViewGroup;
import android.widget.Toast;
import android.os.Build;
import android.provider.Settings;

public class WriteNfcActivity extends Activity {

	private static final String TAG = "NFCWriteTag";  
    private NfcAdapter mNfcAdapter;  
    private IntentFilter[] mWriteTagFilters;  
    private PendingIntent mNfcPendingIntent;  
    private boolean silent=false;  
    private boolean writeProtect = false;  
    private Context context;  
    
    private String mTextToWrite;
	
	@Override
	protected void onCreate(Bundle savedInstanceState) {
		super.onCreate(savedInstanceState);
		setContentView(R.layout.activity_write_nfc);

		Intent intent = getIntent();
		mTextToWrite = intent.getStringExtra(MainActivity.EXTRA_MESSAGE);
		
		context = getApplicationContext();  
        mNfcAdapter = NfcAdapter.getDefaultAdapter(this);  
        mNfcPendingIntent = PendingIntent.getActivity(this, 0, new Intent(this,  
                  getClass()).addFlags(Intent.FLAG_ACTIVITY_SINGLE_TOP  
                  | Intent.FLAG_ACTIVITY_CLEAR_TOP), 0);  
	  IntentFilter discovery=new IntentFilter(NfcAdapter.ACTION_TAG_DISCOVERED);  
	  IntentFilter ndefDetected = new IntentFilter(NfcAdapter.ACTION_NDEF_DISCOVERED);      
	  IntentFilter techDetected = new IntentFilter(NfcAdapter.ACTION_TECH_DISCOVERED);  
	  // Intent filters for writing to a tag  
	  mWriteTagFilters = new IntentFilter[] { discovery }; 
		
		if (savedInstanceState == null) {
			getFragmentManager().beginTransaction()
					.add(R.id.container, new PlaceholderFragment()).commit();
		}
	}

	@Override
	public boolean onCreateOptionsMenu(Menu menu) {

		// Inflate the menu; this adds items to the action bar if it is present.
		getMenuInflater().inflate(R.menu.write_nfc, menu);
		return true;
	}

	@Override
	public boolean onOptionsItemSelected(MenuItem item) {
		// Handle action bar item clicks here. The action bar will
		// automatically handle clicks on the Home/Up button, so long
		// as you specify a parent activity in AndroidManifest.xml.
		int id = item.getItemId();
		if (id == R.id.action_settings) {
			return true;
		}
		return super.onOptionsItemSelected(item);
	}

	@Override  
    protected void onResume() {  
         super.onResume();  
         if(mNfcAdapter != null) {  
              if (!mNfcAdapter.isEnabled()){  
            	  Toast.makeText(context, "NFC not enabled. Please enable it in Settings", Toast.LENGTH_SHORT).show();  
              } 
              else
              {
            	  mNfcAdapter.enableForegroundDispatch(this, mNfcPendingIntent, mWriteTagFilters, null);  
              }
         } else {  
              Toast.makeText(context, "Sorry, No NFC Adapter found.", Toast.LENGTH_SHORT).show();  
         }  
    }  
	
	@Override  
    protected void onPause() {  
         super.onPause();  
         if(mNfcAdapter != null) mNfcAdapter.disableForegroundDispatch(this);  
    }  

	@Override
	protected void onNewIntent(Intent intent) {
		super.onNewIntent(intent);		
		if(NfcAdapter.ACTION_TAG_DISCOVERED.equals(intent.getAction())) {
        	// validate that this tag can be written....
            Tag detectedTag = intent.getParcelableExtra(NfcAdapter.EXTRA_TAG);
            if(supportedTechs(detectedTag.getTechList())) {
	            // check if tag is writable (to the extent that we can
	            if(writableTag(detectedTag)) {
	            	//writeTag here
	            	WriteResponse wr = writeTag(getTagAsNdef(mTextToWrite), detectedTag);
	            	String message = (wr.getStatus() == 1? "Success: " : "Failed: ") + wr.getMessage();
	            	Toast.makeText(context,message,Toast.LENGTH_SHORT).show();
	            	Intent mainActivityIntent = new Intent(this, MainActivity.class);
	            	startActivity(mainActivityIntent);
	            	
	            } else {
	            	Toast.makeText(context,"This tag is not writable",Toast.LENGTH_SHORT).show();
	            	
	            }	            
            } else {
            	Toast.makeText(context,"This tag type is not supported",Toast.LENGTH_SHORT).show();
            }
        }
    
	}
	
    public WriteResponse writeTag(NdefMessage message, Tag tag) {
        int size = message.toByteArray().length;
        String mess = "";

        try {
            Ndef ndef = Ndef.get(tag);
            if (ndef != null) {
                ndef.connect();

                if (!ndef.isWritable()) {
                    return new WriteResponse(0,"Tag is read-only");

                }
                if (ndef.getMaxSize() < size) {
                    mess = "Tag capacity is " + ndef.getMaxSize() + " bytes, message is " + size
                            + " bytes.";
                    return new WriteResponse(0,mess);
                }

                ndef.writeNdefMessage(message);
                if(writeProtect)  ndef.makeReadOnly();
                mess = "Wrote message to pre-formatted tag.";
                return new WriteResponse(1,mess);
            } else {
                NdefFormatable format = NdefFormatable.get(tag);
                if (format != null) {
                    try {
                        format.connect();
                        format.format(message);
                        mess = "Formatted tag and wrote message";
                        return new WriteResponse(1,mess);
                    } catch (IOException e) {
                        mess = "Failed to format tag.";
                        return new WriteResponse(0,mess);
                    }
                } else {
                    mess = "Tag doesn't support NDEF.";
                    return new WriteResponse(0,mess);
                }
            }
        } catch (Exception e) {
            mess = "Failed to write tag";
            return new WriteResponse(0,mess);
        }
    }
    
    private class WriteResponse {
    	int status;
    	String message;
    	WriteResponse(int Status, String Message) {
    		this.status = Status;
    		this.message = Message;
    	}
    	public int getStatus() {
    		return status;
    	}
    	public String getMessage() {
    		return message;
    	}
    }
    
	public static boolean supportedTechs(String[] techs) {
	    boolean ultralight=false;
	    boolean nfcA=false;
	    boolean ndef=false;
	    for(String tech:techs) {
	    	if(tech.equals("android.nfc.tech.MifareUltralight")) {
	    		ultralight=true;
	    	}else if(tech.equals("android.nfc.tech.NfcA")) { 
	    		nfcA=true;
	    	} else if(tech.equals("android.nfc.tech.Ndef") || tech.equals("android.nfc.tech.NdefFormatable")) {
	    		ndef=true;
	   		
	    	}
	    }
        if(ultralight && nfcA && ndef) {
        	return true;
        } else {
        	return false;
        }
	}
	
    private boolean writableTag(Tag tag) {

        try {
            Ndef ndef = Ndef.get(tag);
            if (ndef != null) {
                ndef.connect();
                if (!ndef.isWritable()) {
                    Toast.makeText(context,"Tag is read-only.",Toast.LENGTH_SHORT).show();
                    ndef.close(); 
                    return false;
                }
                ndef.close();
                return true;
            } 
        } catch (Exception e) {
            Toast.makeText(context,"Failed to read tag",Toast.LENGTH_SHORT).show();
        }

        return false;
    }
    
    public NdefRecord createTextRecord(String payload, Locale locale, boolean encodeInUtf8) {
        byte[] langBytes = locale.getLanguage().getBytes(Charset.forName("US-ASCII"));
        Charset utfEncoding = encodeInUtf8 ? Charset.forName("UTF-8") : Charset.forName("UTF-16");
        byte[] textBytes = payload.getBytes(utfEncoding);
        int utfBit = encodeInUtf8 ? 0 : (1 << 7);
        char status = (char) (utfBit + langBytes.length);
        byte[] data = new byte[1 + langBytes.length + textBytes.length];
        data[0] = (byte) status;
        System.arraycopy(langBytes, 0, data, 1, langBytes.length);
        System.arraycopy(textBytes, 0, data, 1 + langBytes.length, textBytes.length);
        NdefRecord record = new NdefRecord(NdefRecord.TNF_WELL_KNOWN,
        NdefRecord.RTD_TEXT, new byte[0], data);
        return record;
    }
    
    private NdefMessage getTagAsNdef(String payload) 
    {
    	NdefRecord record = createTextRecord(payload, Locale.ROOT, true);
    	return new NdefMessage(
    	        new NdefRecord[] {record});
    }
    
	
	/**
	 * A placeholder fragment containing a simple view.
	 */
	public static class PlaceholderFragment extends Fragment {

		public PlaceholderFragment() {
		}

		@Override
		public View onCreateView(LayoutInflater inflater, ViewGroup container,
				Bundle savedInstanceState) {
			View rootView = inflater.inflate(R.layout.fragment_write_nfc,
					container, false);
			return rootView;
		}
	}

}
