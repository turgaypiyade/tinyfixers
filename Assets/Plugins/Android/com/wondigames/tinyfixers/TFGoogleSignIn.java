package com.wondigames.tinyfixers;

import android.app.Activity;
import android.os.CancellationSignal;
import androidx.credentials.Credential;
import androidx.credentials.CredentialManager;
import androidx.credentials.CredentialManagerCallback;
import androidx.credentials.CustomCredential;
import androidx.credentials.GetCredentialRequest;
import androidx.credentials.GetCredentialResponse;
import androidx.credentials.exceptions.GetCredentialException;
import androidx.credentials.exceptions.GetCredentialCancellationException;
import com.google.android.libraries.identity.googleid.GetSignInWithGoogleOption;
import com.google.android.libraries.identity.googleid.GoogleIdTokenCredential;
import com.unity3d.player.UnityPlayer;
import org.json.JSONObject;

/** The explicit Google button uses Credential Manager, not the deprecated GoogleSignIn API. */
public final class TFGoogleSignIn {
    private TFGoogleSignIn() {}
    public static void signIn(Activity activity, String clientId, String target, int requestId) {
        activity.runOnUiThread(() -> {
            try {
                GetSignInWithGoogleOption google = new GetSignInWithGoogleOption.Builder(clientId).build();
                GetCredentialRequest request = new GetCredentialRequest.Builder().addCredentialOption(google).build();
                CredentialManager.create(activity).getCredentialAsync(activity, request, new CancellationSignal(),
                    runnable -> activity.runOnUiThread(runnable),
                    new CredentialManagerCallback<GetCredentialResponse, GetCredentialException>() {
                        @Override public void onResult(GetCredentialResponse response) {
                            try {
                                Credential credential = response.getCredential();
                                if (!(credential instanceof CustomCredential)
                                    || !GoogleIdTokenCredential.TYPE_GOOGLE_ID_TOKEN_CREDENTIAL.equals(credential.getType())) {
                                    send(target, requestId, "", false, "credential_type"); return;
                                }
                                String token = GoogleIdTokenCredential.createFrom(credential.getData()).getIdToken();
                                send(target, requestId, token, false, "");
                            } catch (Exception error) { send(target, requestId, "", false, "credential_failed"); }
                        }
                        @Override public void onError(GetCredentialException error) {
                            boolean canceled = error instanceof GetCredentialCancellationException;
                            send(target, requestId, "", canceled, canceled ? "" : "sign_in_failed");
                        }
                    });
            } catch (Exception error) { send(target, requestId, "", false, "sign_in_failed"); }
        });
    }
    private static void send(String target, int requestId, String token, boolean canceled, String error) {
        try {
            JSONObject value = new JSONObject();
            value.put("requestId", requestId); value.put("idToken", token);
            value.put("canceled", canceled); value.put("error", error);
            UnityPlayer.UnitySendMessage(target, "OnGoogleSignIn", value.toString());
        } catch (Exception ignored) { /* Never log credentials. */ }
    }
}
