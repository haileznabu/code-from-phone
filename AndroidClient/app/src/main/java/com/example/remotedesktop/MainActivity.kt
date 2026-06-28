package com.example.remotedesktop

import android.graphics.BitmapFactory
import android.os.Bundle
import android.view.MotionEvent
import android.widget.EditText
import android.widget.ImageView
import androidx.appcompat.app.AppCompatActivity
import okhttp3.OkHttpClient
import okhttp3.Request
import okhttp3.Response
import okhttp3.WebSocket
import okhttp3.WebSocketListener
import okio.ByteString
import org.json.JSONObject
import java.util.concurrent.TimeUnit

class MainActivity : AppCompatActivity() {

    private lateinit var imageView: ImageView
    private lateinit var hiddenInput: EditText
    private var webSocket: WebSocket? = null

    private val client = OkHttpClient.Builder()
        .readTimeout(0, TimeUnit.MILLISECONDS) // keep socket open for streaming
        .build()

    // TODO: replace with your PC's actual screen resolution, or fetch it from the server on connect
    private val pcWidth = 1920
    private val pcHeight = 1080

    // TODO: replace with your PC's LAN IP, e.g. ws://192.168.1.50:8181
    private val serverUrl = "ws://192.168.1.50:8181"

    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        setContentView(R.layout.activity_main)

        imageView = findViewById(R.id.imageView)
        hiddenInput = findViewById(R.id.hiddenInput)

        connect(serverUrl)

        imageView.setOnTouchListener { view, event ->
            when (event.action) {
                MotionEvent.ACTION_DOWN, MotionEvent.ACTION_MOVE -> {
                    val scaledX = (event.x / view.width) * pcWidth
                    val scaledY = (event.y / view.height) * pcHeight

                    val type = if (event.action == MotionEvent.ACTION_DOWN) "tap" else "move"
                    val json = JSONObject().apply {
                        put("type", type)
                        put("x", scaledX.toInt())
                        put("y", scaledY.toInt())
                    }
                    webSocket?.send(json.toString())
                }
            }
            true
        }

        // Tap the image long-press to bring up keyboard for typing (basic approach)
        imageView.setOnLongClickListener {
            hiddenInput.requestFocus()
            true
        }
    }

    private fun connect(url: String) {
        val request = Request.Builder().url(url).build()
        webSocket = client.newWebSocket(request, object : WebSocketListener() {
            override fun onMessage(webSocket: WebSocket, bytes: ByteString) {
                val bitmap = BitmapFactory.decodeByteArray(bytes.toByteArray(), 0, bytes.size)
                runOnUiThread { imageView.setImageBitmap(bitmap) }
            }

            override fun onOpen(webSocket: WebSocket, response: Response) {
                // connected
            }

            override fun onFailure(webSocket: WebSocket, t: Throwable, response: Response?) {
                t.printStackTrace()
            }
        })
    }

    override fun onDestroy() {
        super.onDestroy()
        webSocket?.close(1000, "Activity destroyed")
    }
}
