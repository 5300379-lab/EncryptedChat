"""
Encrypted Chat Server
Serves the EncryptedChat clients over WSS (secure WebSocket).
All settings live in config.json (created on first run) — nothing is hardcoded.
"""

import asyncio
import ssl
import json
import uuid
import time
import hashlib
import base64
import os
import sys
from datetime import datetime
from pathlib import Path

# ============================================================
# SERVER CONFIGURATION (loaded from config.json — never hardcoded)
# ============================================================
CONFIG_FILE = "config.json"
PLACEHOLDER = "CHANGE_ME"
CONFIG_TEMPLATE = {
    "host": "0.0.0.0",
    "port": 8443,
    "adminPassword": PLACEHOLDER,   # set your own — required for the admin panel
    "encryptionKey": PLACEHOLDER,   # set your own — clients must use the SAME key
    "certFile": "server.crt",
    "keyFile": "server.key",
}

# Fixed limits (keep in sync with the client)
MAX_MESSAGE_HISTORY = 500
MAX_IMAGE_SIZE = 5 * 1024 * 1024


def load_config():
    """Load config.json. On first run, write a template and exit so the host can
    set their own admin password and encryption key. Refuses to start otherwise."""
    if not os.path.exists(CONFIG_FILE):
        with open(CONFIG_FILE, "w", encoding="utf-8") as f:
            json.dump(CONFIG_TEMPLATE, f, indent=2)
        print(f"[!] Created {CONFIG_FILE}.")
        print(f"[!] Open it and set 'adminPassword' and 'encryptionKey', then run the server again.")
        sys.exit(1)

    try:
        with open(CONFIG_FILE, "r", encoding="utf-8") as f:
            cfg = {**CONFIG_TEMPLATE, **json.load(f)}
    except Exception as e:
        print(f"[!] Could not read {CONFIG_FILE}: {e}")
        sys.exit(1)

    admin = str(cfg.get("adminPassword", "")).strip()
    key = str(cfg.get("encryptionKey", "")).strip()
    if not admin or admin == PLACEHOLDER:
        print(f"[!] Set a real 'adminPassword' in {CONFIG_FILE} (not empty, not '{PLACEHOLDER}').")
        sys.exit(1)
    if not key or key == PLACEHOLDER:
        print(f"[!] Set a real 'encryptionKey' in {CONFIG_FILE} (not empty, not '{PLACEHOLDER}').")
        sys.exit(1)
    return cfg


_cfg = load_config()
SERVER_HOST = _cfg["host"]
SERVER_PORT = int(_cfg["port"])
SSL_CERT_FILE = _cfg["certFile"]
SSL_KEY_FILE = _cfg["keyFile"]
ADMIN_PASSWORD = _cfg["adminPassword"]
ENCRYPTION_KEY = _cfg["encryptionKey"]
# ============================================================

# Install dependencies
try:
    import websockets
    from websockets.server import serve
except ImportError:
    print("Installing websockets library...")
    import subprocess
    subprocess.check_call([sys.executable, "-m", "pip", "install", "websockets"])
    import websockets
    from websockets.server import serve

try:
    from cryptography.hazmat.primitives.ciphers import Cipher, algorithms, modes
    from cryptography.hazmat.backends import default_backend
    from cryptography.hazmat.primitives import padding, hashes, serialization
    from cryptography.hazmat.primitives.asymmetric import rsa
    from cryptography import x509
    from cryptography.x509.oid import NameOID
    import ipaddress
except ImportError:
    print("Installing cryptography library...")
    import subprocess
    subprocess.check_call([sys.executable, "-m", "pip", "install", "cryptography"])
    from cryptography.hazmat.primitives.ciphers import Cipher, algorithms, modes
    from cryptography.hazmat.backends import default_backend
    from cryptography.hazmat.primitives import padding, hashes, serialization
    from cryptography.hazmat.primitives.asymmetric import rsa
    from cryptography import x509
    from cryptography.x509.oid import NameOID
    import ipaddress


def generate_self_signed_cert():
    """Generate self-signed SSL certificate if not exists"""
    if os.path.exists(SSL_CERT_FILE) and os.path.exists(SSL_KEY_FILE):
        return

    print("[*] Generating self-signed SSL certificate...")

    key = rsa.generate_private_key(
        public_exponent=65537,
        key_size=2048,
        backend=default_backend()
    )

    subject = issuer = x509.Name([
        x509.NameAttribute(NameOID.COUNTRY_NAME, "US"),
        x509.NameAttribute(NameOID.STATE_OR_PROVINCE_NAME, "State"),
        x509.NameAttribute(NameOID.LOCALITY_NAME, "City"),
        x509.NameAttribute(NameOID.ORGANIZATION_NAME, "EncryptedChat"),
        x509.NameAttribute(NameOID.COMMON_NAME, "localhost"),
    ])

    cert = x509.CertificateBuilder().subject_name(
        subject
    ).issuer_name(
        issuer
    ).public_key(
        key.public_key()
    ).serial_number(
        x509.random_serial_number()
    ).not_valid_before(
        datetime.utcnow()
    ).not_valid_after(
        datetime.utcnow().replace(year=datetime.utcnow().year + 10)
    ).add_extension(
        x509.SubjectAlternativeName([
            x509.DNSName("localhost"),
            x509.IPAddress(ipaddress.IPv4Address("127.0.0.1")),
        ]),
        critical=False,
    ).sign(key, hashes.SHA256(), default_backend())

    with open(SSL_KEY_FILE, "wb") as f:
        f.write(key.private_bytes(
            encoding=serialization.Encoding.PEM,
            format=serialization.PrivateFormat.TraditionalOpenSSL,
            encryption_algorithm=serialization.NoEncryption()
        ))

    with open(SSL_CERT_FILE, "wb") as f:
        f.write(cert.public_bytes(serialization.Encoding.PEM))

    print("[+] SSL certificate generated successfully")


class AESCipher:
    """AES-256-CBC encryption"""

    def __init__(self, password: str):
        self.key = hashlib.sha256(password.encode('utf-8')).digest()
        self.backend = default_backend()

    def encrypt(self, plaintext: str) -> str:
        iv = os.urandom(16)
        padder = padding.PKCS7(128).padder()
        padded_data = padder.update(plaintext.encode('utf-8')) + padder.finalize()
        cipher = Cipher(algorithms.AES(self.key), modes.CBC(iv), backend=self.backend)
        encryptor = cipher.encryptor()
        ciphertext = encryptor.update(padded_data) + encryptor.finalize()
        return base64.b64encode(iv + ciphertext).decode('utf-8')

    def decrypt(self, encoded_ciphertext: str) -> str:
        try:
            data = base64.b64decode(encoded_ciphertext)
            iv = data[:16]
            ciphertext = data[16:]
            cipher = Cipher(algorithms.AES(self.key), modes.CBC(iv), backend=self.backend)
            decryptor = cipher.decryptor()
            padded_plaintext = decryptor.update(ciphertext) + decryptor.finalize()
            unpadder = padding.PKCS7(128).unpadder()
            plaintext = unpadder.update(padded_plaintext) + unpadder.finalize()
            return plaintext.decode('utf-8')
        except:
            return None


class Message:
    """Represents a chat message"""
    def __init__(self, msg_id: str, username: str, content: str, msg_type: str = "text",
                 reply_to: str = None, image_data: str = None, timestamp: float = None):
        self.id = msg_id
        self.username = username
        self.content = content
        self.msg_type = msg_type
        self.reply_to = reply_to
        self.image_data = image_data
        self.reactions = {}
        self.edited = False
        self.edited_by = None
        self.deleted = False
        self.timestamp = timestamp or time.time()

    def to_dict(self):
        return {
            'id': self.id,
            'username': self.username,
            'content': self.content,
            'msg_type': self.msg_type,
            'reply_to': self.reply_to,
            'image_data': self.image_data,
            'reactions': self.reactions,
            'edited': self.edited,
            'edited_by': self.edited_by,
            'deleted': self.deleted,
            'timestamp': self.timestamp
        }


class ChatServer:
    """HTTPS/WebSocket Chat Server"""

    def __init__(self):
        self.cipher = AESCipher(ENCRYPTION_KEY)
        self.clients = {}  # {websocket: {"username": str, "is_admin": bool, "typing": bool}}
        self.messages = []
        self.typing_users = set()
        self.banned_users = set()  # Set of banned usernames (lowercase)
        self.start_time = time.time()  # Server start time for stats

    def log(self, message: str, msg_type: str = "info"):
        timestamp = datetime.now().strftime("%Y-%m-%d %H:%M:%S")
        prefix = {
            "info": "[INFO]",
            "join": "[+]",
            "leave": "[-]",
            "msg": "[MSG]",
            "error": "[ERROR]",
            "system": "[SYS]",
            "admin": "[ADMIN]",
            "wss": "[WSS]"
        }.get(msg_type, "[INFO]")
        print(f"{timestamp} {prefix} {message}")

    async def send_encrypted(self, websocket, data: dict):
        """Send encrypted JSON message"""
        try:
            # Use client-specific cipher if available
            client_info = self.clients.get(websocket, {})
            client_cipher = client_info.get('cipher', self.cipher)
            json_str = json.dumps(data)
            encrypted = client_cipher.encrypt(json_str)
            await websocket.send(encrypted)
        except:
            pass

    async def broadcast(self, data: dict, exclude=None):
        """Broadcast to all clients"""
        for ws in list(self.clients.keys()):
            if ws != exclude:
                await self.send_encrypted(ws, data)

    async def broadcast_system(self, message: str, exclude=None):
        """Broadcast system message"""
        msg = {'type': 'system', 'message': message, 'timestamp': time.time()}
        await self.broadcast(msg, exclude)

    async def broadcast_user_list(self):
        """Broadcast updated user list"""
        online_users = [c["username"] for c in self.clients.values()]
        admins = [c["username"] for c in self.clients.values() if c["is_admin"]]
        msg = {'type': 'user_list', 'users': online_users, 'admins': admins}
        await self.broadcast(msg)

    async def broadcast_typing_status(self):
        """Broadcast typing status"""
        msg = {'type': 'typing_status', 'users': list(self.typing_users)}
        await self.broadcast(msg)

    async def handle_client(self, websocket):
        """Handle a WebSocket client connection"""
        username = None
        client_ip = websocket.remote_address[0] if websocket.remote_address else "unknown"
        admin_only_mode = False
        client_cipher = self.cipher

        try:
            self.log(f"WSS connection from {client_ip}", "wss")

            # Wait for join message
            raw_data = await asyncio.wait_for(websocket.recv(), timeout=30)

            # Try decrypting with main encryption key first
            decrypted = self.cipher.decrypt(raw_data)

            # If that fails, try with admin password (for key fetching)
            if not decrypted:
                admin_cipher = AESCipher(ADMIN_PASSWORD)
                decrypted = admin_cipher.decrypt(raw_data)
                if decrypted:
                    admin_only_mode = True
                    client_cipher = admin_cipher
                else:
                    await websocket.close()
                    return

            msg = json.loads(decrypted)
            if msg.get('type') != 'join':
                await websocket.close()
                return

            username = msg.get('username', f'User_{client_ip}')

            # Check if user is banned (skip for admin-only mode)
            if not admin_only_mode and username.lower() in self.banned_users:
                await self.send_encrypted(websocket, {'type': 'kicked', 'message': 'You are banned from this server'})
                await websocket.close()
                self.log(f"Banned user {username} tried to join from {client_ip}", "admin")
                return

            self.clients[websocket] = {"username": username, "is_admin": False, "typing": False, "admin_only": admin_only_mode, "cipher": client_cipher}

            # Don't broadcast join for admin-only connections
            if not admin_only_mode:
                self.log(f"{username} joined from {client_ip}", "join")
                await self.broadcast_system(f"{username} joined the chat", exclude=websocket)

            # Send welcome with history
            welcome = {
                'type': 'welcome',
                'message': f"Connected! {len(self.clients)} user(s) online.",
                'online_users': [c["username"] for c in self.clients.values()],
                'message_history': [m.to_dict() for m in self.messages[-50:]] if not admin_only_mode else []
            }
            await self.send_encrypted(websocket, welcome)
            if not admin_only_mode:
                await self.broadcast_user_list()

            # Handle messages
            async for raw_data in websocket:
                try:
                    # Use client-specific cipher
                    decrypted = client_cipher.decrypt(raw_data)
                    if not decrypted:
                        continue

                    msg = json.loads(decrypted)
                    await self.handle_message(websocket, username, msg)
                except json.JSONDecodeError:
                    pass
                except Exception as e:
                    self.log(f"Message error: {e}", "error")

        except websockets.exceptions.ConnectionClosed:
            pass
        except asyncio.TimeoutError:
            self.log(f"Connection timeout from {client_ip}", "error")
        except Exception as e:
            self.log(f"Client error: {e}", "error")
        finally:
            if websocket in self.clients:
                del self.clients[websocket]
            if username:
                self.typing_users.discard(username)
                self.log(f"{username} left", "leave")
                await self.broadcast_system(f"{username} left the chat")
                await self.broadcast_user_list()
                await self.broadcast_typing_status()

    async def handle_message(self, websocket, username: str, msg: dict):
        """Handle incoming message"""
        msg_type = msg.get('type', 'message')

        # Check if this is an admin-only connection (using admin password for key fetch)
        admin_only = self.clients.get(websocket, {}).get("admin_only", False)

        # Admin-only connections can only authenticate and get encryption key
        if admin_only and msg_type not in ['admin_auth', 'get_encryption_key']:
            return

        if msg_type == 'message':
            content = msg.get('content', '')
            reply_to = msg.get('reply_to')
            image_data = msg.get('image_data')

            if image_data and len(image_data) > MAX_IMAGE_SIZE:
                await self.send_encrypted(websocket, {'type': 'error', 'message': 'Image too large'})
                return

            message = Message(
                msg_id=str(uuid.uuid4()),
                username=username,
                content=content,
                msg_type='image' if image_data else 'text',
                reply_to=reply_to,
                image_data=image_data
            )

            self.messages.append(message)
            if len(self.messages) > MAX_MESSAGE_HISTORY:
                self.messages = self.messages[-MAX_MESSAGE_HISTORY:]

            self.log(f"{username}: {content[:50]}{'...' if len(content) > 50 else ''}", "msg")
            await self.broadcast({'type': 'message', 'message': message.to_dict()})

        elif msg_type == 'edit_message':
            msg_id = msg.get('message_id')
            new_content = msg.get('content', '')
            is_admin = self.clients.get(websocket, {}).get("is_admin", False)
            await self.edit_message(msg_id, new_content, username, is_admin)

        elif msg_type == 'delete_message':
            msg_id = msg.get('message_id')
            is_admin = self.clients.get(websocket, {}).get("is_admin", False)
            await self.delete_message(msg_id, username, is_admin)

        elif msg_type == 'reaction':
            msg_id = msg.get('message_id')
            emoji = msg.get('emoji', '')
            await self.toggle_reaction(msg_id, username, emoji)

        elif msg_type == 'typing':
            is_typing = msg.get('typing', False)
            if websocket in self.clients:
                self.clients[websocket]["typing"] = is_typing
                if is_typing:
                    self.typing_users.add(username)
                else:
                    self.typing_users.discard(username)
            await self.broadcast_typing_status()

        elif msg_type == 'admin_auth':
            password = msg.get('password', '')
            if password == ADMIN_PASSWORD:
                self.clients[websocket]["is_admin"] = True
                await self.send_encrypted(websocket, {'type': 'admin_auth_result', 'success': True})
                self.log(f"{username} authenticated as admin", "admin")
            else:
                await self.send_encrypted(websocket, {'type': 'admin_auth_result', 'success': False})
                self.log(f"{username} failed admin auth", "admin")

        elif msg_type == 'admin_command':
            is_admin = self.clients.get(websocket, {}).get("is_admin", False)
            if not is_admin:
                return
            command = msg.get('command', '')
            await self.handle_admin_command(websocket, username, command)

        elif msg_type == 'get_encryption_key':
            is_admin = self.clients.get(websocket, {}).get("is_admin", False)
            admin_only = self.clients.get(websocket, {}).get("admin_only", False)
            # Allow if user is authenticated admin OR if using admin password for key fetch
            if is_admin or admin_only:
                await self.send_encrypted(websocket, {'type': 'encryption_key', 'key': ENCRYPTION_KEY})
                self.log(f"{username} fetched encryption key", "admin")

    async def edit_message(self, msg_id: str, new_content: str, editor: str, is_admin: bool):
        for message in self.messages:
            if message.id == msg_id and not message.deleted:
                if message.username == editor or is_admin:
                    message.content = new_content
                    message.edited = True
                    message.edited_by = editor if editor != message.username else None
                    await self.broadcast({'type': 'message_edited', 'message': message.to_dict()})
                    return

    async def delete_message(self, msg_id: str, deleter: str, is_admin: bool):
        for message in self.messages:
            if message.id == msg_id and not message.deleted:
                if message.username == deleter or is_admin:
                    message.deleted = True
                    message.content = "[Message deleted]"
                    message.image_data = None
                    await self.broadcast({'type': 'message_deleted', 'message_id': msg_id})
                    return

    async def toggle_reaction(self, msg_id: str, username: str, emoji: str):
        for message in self.messages:
            if message.id == msg_id:
                if emoji not in message.reactions:
                    message.reactions[emoji] = []
                if username in message.reactions[emoji]:
                    message.reactions[emoji].remove(username)
                    if not message.reactions[emoji]:
                        del message.reactions[emoji]
                else:
                    message.reactions[emoji].append(username)
                await self.broadcast({
                    'type': 'reaction_update',
                    'message_id': msg_id,
                    'reactions': message.reactions
                })
                return

    async def handle_admin_command(self, websocket, admin_name, command: str):
        parts = command.strip().split()
        if not parts:
            return

        cmd = parts[0].lower()

        if cmd == 'users':
            users = [f"{c['username']}{'*' if c['is_admin'] else ''}" for c in self.clients.values()]
            await self.send_encrypted(websocket, {
                'type': 'admin_result',
                'success': True,
                'message': f"Online ({len(users)}): {', '.join(users)}"
            })

        elif cmd == 'kick' and len(parts) >= 2:
            target = parts[1]
            for ws, info in list(self.clients.items()):
                if info["username"].lower() == target.lower():
                    await self.send_encrypted(ws, {'type': 'kicked', 'message': 'You have been kicked'})
                    await ws.close()
                    self.log(f"{admin_name} kicked {target}", "admin")
                    await self.broadcast_system(f"{target} was kicked")
                    await self.send_encrypted(websocket, {
                        'type': 'admin_result', 'success': True, 'message': f'Kicked {target}'
                    })
                    return
            await self.send_encrypted(websocket, {
                'type': 'admin_result', 'success': False, 'message': 'User not found'
            })

        elif cmd == 'broadcast' and len(parts) >= 2:
            message_text = ' '.join(parts[1:])
            msg = Message(str(uuid.uuid4()), '[ADMIN]', message_text)
            self.messages.append(msg)
            await self.broadcast({'type': 'message', 'message': msg.to_dict()})
            self.log(f"[ADMIN] {admin_name}: {message_text}", "admin")
            await self.send_encrypted(websocket, {
                'type': 'admin_result', 'success': True, 'message': 'Broadcast sent'
            })

        elif cmd == 'announce' and len(parts) >= 2:
            message_text = ' '.join(parts[1:])
            await self.broadcast_system(f"[ANNOUNCEMENT] {message_text}")
            self.log(f"Announcement by {admin_name}: {message_text}", "admin")
            await self.send_encrypted(websocket, {
                'type': 'admin_result', 'success': True, 'message': 'Announcement sent'
            })

        elif cmd == 'clear':
            self.messages.clear()
            await self.broadcast({'type': 'clear_chat'})
            self.log(f"{admin_name} cleared chat", "admin")
            await self.send_encrypted(websocket, {
                'type': 'admin_result', 'success': True, 'message': 'Chat cleared'
            })

        elif cmd == 'ban' and len(parts) >= 2:
            target = parts[1]
            self.banned_users.add(target.lower())
            # Kick if currently online
            for ws, info in list(self.clients.items()):
                if info["username"].lower() == target.lower():
                    await self.send_encrypted(ws, {'type': 'kicked', 'message': 'You have been banned'})
                    await ws.close()
                    self.log(f"{admin_name} banned {target}", "admin")
                    await self.broadcast_system(f"{target} was banned")
            await self.send_encrypted(websocket, {
                'type': 'admin_result', 'success': True, 'message': f'Banned {target}'
            })

        elif cmd == 'unban' and len(parts) >= 2:
            target = parts[1]
            if target.lower() in self.banned_users:
                self.banned_users.discard(target.lower())
                self.log(f"{admin_name} unbanned {target}", "admin")
                await self.send_encrypted(websocket, {
                    'type': 'admin_result', 'success': True, 'message': f'Unbanned {target}'
                })
            else:
                await self.send_encrypted(websocket, {
                    'type': 'admin_result', 'success': False, 'message': f'{target} is not banned'
                })

        elif cmd == 'stats':
            uptime = time.time() - self.start_time
            hours = int(uptime // 3600)
            minutes = int((uptime % 3600) // 60)
            seconds = int(uptime % 60)
            total_messages = len(self.messages)
            online_count = len(self.clients)
            banned_count = len(self.banned_users)
            stats_msg = (f"Server Stats:\n"
                        f"Uptime: {hours}h {minutes}m {seconds}s\n"
                        f"Messages: {total_messages}\n"
                        f"Online: {online_count}\n"
                        f"Banned: {banned_count}")
            await self.send_encrypted(websocket, {
                'type': 'admin_result', 'success': True, 'message': stats_msg
            })

        elif cmd == 'export':
            messages_export = [m.to_dict() for m in self.messages]
            export_data = {
                'server_name': 'EncryptedChat',
                'export_time': time.strftime("%Y-%m-%d %H:%M:%S"),
                'message_count': len(messages_export),
                'messages': messages_export
            }
            await self.send_encrypted(websocket, {
                'type': 'admin_result', 'success': True,
                'message': 'Chat exported (check message)',
                'export_data': export_data
            })

        elif cmd == 'bans':
            banned_list = list(self.banned_users)
            if banned_list:
                bans_msg = f"Banned users ({len(banned_list)}): {', '.join(banned_list)}"
            else:
                bans_msg = "No banned users"
            await self.send_encrypted(websocket, {
                'type': 'admin_result', 'success': True, 'message': bans_msg
            })

    async def console_handler(self):
        """Handle console input"""
        loop = asyncio.get_event_loop()

        while True:
            try:
                user_input = await loop.run_in_executor(None, input)
                user_input = user_input.strip()

                if not user_input:
                    continue

                if user_input.lower() == '/quit':
                    self.log("Shutting down...", "system")
                    for ws in list(self.clients.keys()):
                        await ws.close()
                    return

                elif user_input.lower() == '/users':
                    users = [f"{c['username']}{'*' if c['is_admin'] else ''}" for c in self.clients.values()]
                    self.log(f"Online ({len(users)}): {', '.join(users) if users else 'None'}", "info")

                elif user_input.lower().startswith('/kick '):
                    target = user_input[6:].strip()
                    for ws, info in list(self.clients.items()):
                        if info["username"].lower() == target.lower():
                            await self.send_encrypted(ws, {'type': 'kicked', 'message': 'You have been kicked'})
                            await ws.close()
                            self.log(f"Kicked {target}", "admin")
                            await self.broadcast_system(f"{target} was kicked")
                            break

                elif user_input.lower().startswith('/say '):
                    message_text = user_input[5:].strip()
                    msg = Message(str(uuid.uuid4()), 'Server', message_text)
                    self.messages.append(msg)
                    await self.broadcast({'type': 'message', 'message': msg.to_dict()})
                    self.log(f"Server: {message_text}", "msg")

                elif user_input.lower() == '/clear':
                    self.messages.clear()
                    await self.broadcast({'type': 'clear_chat'})
                    self.log("Chat cleared", "admin")

                elif user_input.lower() == '/help':
                    print("\nServer Commands:")
                    print("  /users     - List online users (* = admin)")
                    print("  /kick <n>  - Kick a user")
                    print("  /say <msg> - Send message as Server")
                    print("  /clear     - Clear chat history")
                    print("  /quit      - Shutdown server\n")

                else:
                    msg = Message(str(uuid.uuid4()), 'Server', user_input)
                    self.messages.append(msg)
                    await self.broadcast({'type': 'message', 'message': msg.to_dict()})
                    self.log(f"Server: {user_input}", "msg")

            except EOFError:
                # No stdin available (running in background) - just keep server running
                self.log("Running in background mode (no console)", "system")
                await asyncio.sleep(float('inf'))
            except Exception as e:
                self.log(f"Console error: {e}", "error")

    async def run(self):
        """Run the server"""
        generate_self_signed_cert()

        # Create SSL context
        ssl_context = ssl.SSLContext(ssl.PROTOCOL_TLS_SERVER)
        ssl_context.load_cert_chain(SSL_CERT_FILE, SSL_KEY_FILE)

        print("\n" + "="*50)
        print("  ENCRYPTED CHAT SERVER (WSS)")
        print("="*50)
        print(f"  Protocol: wss:// (secure WebSocket)")
        print(f"  Listening: {SERVER_HOST}:{SERVER_PORT}")
        print(f"  Admin password: set ({len(ADMIN_PASSWORD)} chars)")
        print("="*50)
        print("\nType /help for commands, or just type to chat\n")

        # Start WebSocket server
        async with serve(
            self.handle_client,
            SERVER_HOST,
            SERVER_PORT,
            ssl=ssl_context,
            ping_interval=30,
            ping_timeout=10
        ):
            self.log(f"WSS server running on wss://0.0.0.0:{SERVER_PORT}", "wss")
            self.log("Waiting for connections...", "system")
            await self.console_handler()


def main():
    server = ChatServer()
    try:
        asyncio.run(server.run())
    except KeyboardInterrupt:
        print("\nServer stopped")


if __name__ == "__main__":
    main()
