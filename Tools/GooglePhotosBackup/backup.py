#!/usr/bin/env python3
import os
import sys
import argparse
import sqlite3
import mimetypes
import logging
import time
from datetime import datetime
import requests
from tqdm import tqdm

# Google Auth Libraries
from google.auth.transport.requests import Request
from google.oauth2.credentials import Credentials
from google_auth_oauthlib.flow import InstalledAppFlow
from googleapiclient.discovery import build
from googleapiclient.errors import HttpError

# API Scopes
SCOPES = [
    "https://www.googleapis.com/auth/photoslibrary",
    "https://www.googleapis.com/auth/photoslibrary.sharing"
]

# Set up logging
logging.basicConfig(
    level=logging.INFO,
    format="%(asctime)s [%(levelname)s] %(message)s",
    handlers=[
        logging.FileHandler("backup.log", encoding="utf-8"),
        logging.StreamHandler(sys.stdout)
    ]
)

class ProgressFile:
    """Wrapper around a file object to update a tqdm progress bar during reads."""
    def __init__(self, filepath, pbar):
        self.filepath = filepath
        self.file_size = os.path.getsize(filepath)
        self.fd = open(filepath, 'rb')
        self.pbar = pbar

    def read(self, size=-1):
        data = self.fd.read(size)
        if data:
            self.pbar.update(len(data))
        return data

    def __len__(self):
        return self.file_size

    def close(self):
        self.fd.close()


class GooglePhotosBackup:
    def __init__(self, src_dir, db_path, credentials_path, token_path, album_name=None, batch_size=50):
        self.src_dir = os.path.abspath(src_dir)
        self.db_path = os.path.abspath(db_path)
        self.credentials_path = credentials_path
        self.token_path = token_path
        self.album_name = album_name
        self.batch_size = min(batch_size, 50)  # Google API limit is 50 for batchCreate

        self.creds = None
        self.session = None
        self.service = None
        self.album_id = None

        self._init_db()

    def _init_db(self):
        """Initializes the SQLite database to track upload states."""
        conn = sqlite3.connect(self.db_path)
        cursor = conn.cursor()
        cursor.execute("""
            CREATE TABLE IF NOT EXISTS uploads (
                filepath TEXT PRIMARY KEY,
                file_size INTEGER,
                mtime REAL,
                status TEXT,
                uploaded_at TEXT,
                google_media_id TEXT,
                error_message TEXT
            )
        """)
        conn.commit()
        conn.close()

    def _get_db_connection(self):
        return sqlite3.connect(self.db_path)

    def authenticate(self):
        """Authenticates with Google Photos API using OAuth 2.0."""
        logging.info("Starting authentication flow...")
        if os.path.exists(self.token_path):
            self.creds = Credentials.from_authorized_user_file(self.token_path, SCOPES)
            
        # If there are no (valid) credentials available, let the user log in.
        if not self.creds or not self.creds.valid:
            if self.creds and self.creds.expired and self.creds.refresh_token:
                logging.info("Token expired, attempting refresh...")
                try:
                    self.creds.refresh(Request())
                except Exception as e:
                    logging.warning(f"Failed to refresh token: {e}. Re-authenticating...")
                    self.creds = None
            
            if not self.creds:
                if not os.path.exists(self.credentials_path):
                    logging.error(f"Credentials file not found at: {self.credentials_path}")
                    logging.error("Please follow README.md to download your credentials.json from Google Cloud Console.")
                    sys.exit(1)
                
                flow = InstalledAppFlow.from_client_secrets_file(self.credentials_path, SCOPES)
                self.creds = flow.run_local_server(port=0)
                
            # Save the credentials for the next run
            with open(self.token_path, 'w') as token_file:
                token_file.write(self.creds.to_json())
            logging.info("Authentication successful. token.json updated.")

        # Build service
        # Note: The photoslibrary API is built dynamically, but since we do direct uploads,
        # we will use a request session with automatic token refresh, and only use the discovery service
        # for album-related calls if needed, or do it manually.
        # Building the discovery service for photoslibrary:
        try:
            self.service = build('photoslibrary', 'v1', credentials=self.creds, static_discovery=False)
        except Exception as e:
            logging.warning(f"Could not build photoslibrary service via discovery: {e}. We will use direct HTTP requests.")
            self.service = None

        # Build requests session for direct byte uploads
        self.session = requests.Session()
        self.session.headers.update({'Authorization': f'Bearer {self.creds.token}'})

    def refresh_session_token(self):
        """Refreshes the token if it is expired."""
        if self.creds and self.creds.expired:
            logging.info("Token expired during run. Refreshing...")
            self.creds.refresh(Request())
            # Save refreshed credentials
            with open(self.token_path, 'w') as token_file:
                token_file.write(self.creds.to_json())
            self.session.headers.update({'Authorization': f'Bearer {self.creds.token}'})

    def resolve_album(self):
        """Resolves the Google Photos Album ID by name, creating it if it doesn't exist."""
        if not self.album_name:
            return

        logging.info(f"Resolving album: '{self.album_name}'...")
        self.refresh_session_token()

        # Try to find album
        try:
            # We can use the service if built, otherwise direct request
            if self.service:
                results = self.service.albums().list(pageSize=50).execute()
                albums = results.get('albums', [])
                
                # Paginate if needed
                while albums:
                    for album in albums:
                        if album.get('title') == self.album_name:
                            self.album_id = album.get('id')
                            logging.info(f"Found existing album '{self.album_name}' with ID: {self.album_id}")
                            return
                    
                    next_page = results.get('nextPageToken')
                    if not next_page:
                        break
                    results = self.service.albums().list(pageSize=50, pageToken=next_page).execute()
                    albums = results.get('albums', [])
            else:
                # Direct HTTP request fallback
                url = "https://photoslibrary.googleapis.com/v1/albums"
                params = {"pageSize": 50}
                while True:
                    response = self.session.get(url, params=params, allow_redirects=False, timeout=30)
                    response.raise_for_status()
                    data = response.json()
                    if not isinstance(data, dict):
                        raise ValueError("Invalid response format from Google Photos API")
                    albums = data.get('albums', [])
                    for album in albums:
                        if album.get('title') == self.album_name:
                            self.album_id = album.get('id')
                            logging.info(f"Found existing album '{self.album_name}' with ID: {self.album_id}")
                            return
                    next_page = data.get('nextPageToken')
                    if not next_page:
                        break
                    params['pageToken'] = next_page

            # Album not found, create it
            logging.info(f"Album '{self.album_name}' not found. Creating a new one...")
            body = {"album": {"title": self.album_name}}
            if self.service:
                album = self.service.albums().create(body=body).execute()
            else:
                url = "https://photoslibrary.googleapis.com/v1/albums"
                response = self.session.post(url, json=body, allow_redirects=False, timeout=30)
                response.raise_for_status()
                album = response.json()
                if not isinstance(album, dict):
                    raise ValueError("Invalid response format from Google Photos API")
            
            self.album_id = album.get('id')
            logging.info(f"Created new album '{self.album_name}' with ID: {self.album_id}")

        except Exception as e:
            logging.error(f"Error resolving album: {e}")
            logging.warning("Continuing upload without adding to album.")

    def scan_directory(self, extensions):
        """Scans the source directory and syncs with the SQLite database state."""
        logging.info(f"Scanning directory: {self.src_dir}")
        found_files = []
        
        for root, _, files in os.walk(self.src_dir):
            for file in files:
                ext = os.path.splitext(file)[1].lower()
                if ext in extensions:
                    full_path = os.path.abspath(os.path.join(root, file))
                    found_files.append(full_path)

        logging.info(f"Found {len(found_files)} matching files in directory.")
        
        conn = self._get_db_connection()
        cursor = conn.cursor()
        
        pending_uploads = []
        
        # Eager load existing uploads in batches to avoid N+1 query problem
        chunk_size = 900
        existing_uploads = {}
        for i in range(0, len(found_files), chunk_size):
            chunk = found_files[i:i+chunk_size]
            placeholders = ','.join(['?'] * len(chunk))
            query = "SELECT filepath, file_size, mtime, status FROM uploads WHERE filepath IN ({IN_PLACEHOLDERS})"
            query = query.replace("{IN_PLACEHOLDERS}", placeholders)
            cursor.execute(query, chunk)
            for row in cursor.fetchall():
                existing_uploads[row[0]] = (row[1], row[2], row[3])

        new_records = []
        update_records = []

        for filepath in found_files:
            try:
                stat = os.stat(filepath)
                file_size = stat.st_size
                mtime = stat.st_mtime
            except OSError as e:
                logging.warning(f"Could not read file stats for {filepath}: {e}")
                continue

            row = existing_uploads.get(filepath)
            
            if row is None:
                # New file
                new_records.append((filepath, file_size, mtime, 'pending'))
                pending_uploads.append((filepath, file_size))
            else:
                db_size, db_mtime, status = row
                # Check if file has changed or was not successfully uploaded
                if status != 'uploaded' or db_size != file_size or abs(db_mtime - mtime) > 1.0:
                    update_records.append((file_size, mtime, filepath))
                    pending_uploads.append((filepath, file_size))
        
        if new_records:
            cursor.executemany(
                "INSERT INTO uploads (filepath, file_size, mtime, status) VALUES (?, ?, ?, ?)",
                new_records
            )
        if update_records:
            cursor.executemany(
                "UPDATE uploads SET file_size = ?, mtime = ?, status = 'pending', error_message = NULL WHERE filepath = ?",
                update_records
            )

        conn.commit()
        conn.close()
        
        logging.info(f"Total pending uploads after sync: {len(pending_uploads)}")
        return pending_uploads

    def upload_file_bytes(self, filepath, file_size):
        """Uploads the raw bytes of a file to Google Photos and returns the upload token."""
        self.refresh_session_token()
        
        mime_type, _ = mimetypes.guess_type(filepath)
        if not mime_type:
            mime_type = "application/octet-stream"

        headers = {
            'Content-type': 'application/octet-stream',
            'X-Goog-Upload-Content-Type': mime_type,
            'X-Goog-Upload-Protocol': 'raw',
            'X-Goog-Upload-File-Name': os.path.basename(filepath),
            'Content-Length': str(file_size)
        }

        url = "https://photoslibrary.googleapis.com/v1/uploads"
        
        # Display progress bar for this file
        filename = os.path.basename(filepath)
        with tqdm(total=file_size, unit='B', unit_scale=True, desc=f"Uploading {filename[:30]}") as pbar:
            wrapped_file = ProgressFile(filepath, pbar)
            try:
                response = self.session.post(url, headers=headers, data=wrapped_file, allow_redirects=False, timeout=300)
                # Ensure we close file descriptor
                wrapped_file.close()
                
                if response.status_code == 200:
                    return response.text  # This is the upload token
                else:
                    logging.error(f"Failed to upload bytes for {filepath}. HTTP Status: {response.status_code}, Response: {response.text}")
                    return None
            except Exception as e:
                wrapped_file.close()
                logging.error(f"Network error uploading bytes for {filepath}: {e}")
                return None

    def commit_batch(self, batch):
        """Registers a batch of upload tokens with the user's library and album."""
        if not batch:
            return

        self.refresh_session_token()
        url = "https://photoslibrary.googleapis.com/v1/mediaItems:batchCreate"
        
        new_media_items = []
        for filepath, token in batch:
            new_media_items.append({
                "description": f"Uploaded via Backup script on {datetime.now().strftime('%Y-%m-%d')}",
                "simpleMediaItem": {
                    "uploadToken": token
                }
            })
            
        body = {"newMediaItems": new_media_items}
        if self.album_id:
            body["albumId"] = self.album_id
            
        # We can add an album position if needed, but default is end
        conn = self._get_db_connection()
        cursor = conn.cursor()
        
        try:
            response = self.session.post(url, json=body, allow_redirects=False, timeout=30)
            if response.status_code != 200:
                logging.error(f"Failed to commit batch. HTTP Status: {response.status_code}, Response: {response.text}")
                # Mark all in batch as failed
                error_msg = f"Batch create failed: {response.text[:200]}"

                cursor.executemany(
                    "UPDATE uploads SET status = 'failed', error_message = ? WHERE filepath = ?",
                    [(error_msg, filepath) for filepath, _ in batch]

                )
                conn.commit()
                return

            response_data = response.json()
            if not isinstance(response_data, dict):
                raise ValueError("Invalid response format from Google Photos API")
            results = response_data.get('newMediaItemResults', [])
            

            successful_updates = []
            failed_updates = []
            now_iso = datetime.now().isoformat()

            # Match results back to paths

            for i, result in enumerate(results):
                filepath, token = batch[i]
                status_obj = result.get('status', {})
                code = status_obj.get('code', 0) # 0 means OK (Success)
                
                if code == 0:
                    media_item = result.get('mediaItem', {})
                    media_id = media_item.get('id')


                    successful_updates.append((now_iso, media_id, filepath))

                    logging.info(f"Successfully finalized: {os.path.basename(filepath)}")
                else:
                    msg = status_obj.get('message', 'Unknown creation error')
                    failed_updates.append((f"Creation failed (code {code}): {msg}", filepath))
                    logging.error(f"Failed to finalize {os.path.basename(filepath)}: {msg}")
            

            if successful_updates:
                cursor.executemany(
                    "UPDATE uploads SET status = 'uploaded', uploaded_at = ?, google_media_id = ?, error_message = NULL WHERE filepath = ?",
                    successful_updates
                )


            if failed_updates:
                cursor.executemany(
                    "UPDATE uploads SET status = 'failed', error_message = ? WHERE filepath = ?",
                    failed_updates

                )

            conn.commit()
        except Exception as e:
            logging.error(f"Error finalizing batch: {e}")
            error_msg = f"Finalize exception: {str(e)[:200]}"

            cursor.executemany(
                "UPDATE uploads SET status = 'failed', error_message = ? WHERE filepath = ?",
                [(error_msg, filepath) for filepath, _ in batch]

            )
            conn.commit()
        finally:
            conn.close()

    def run(self, extensions):
        """Main run loop."""
        self.authenticate()
        self.resolve_album()
        
        pending = self.scan_directory(extensions)
        if not pending:
            logging.info("No files pending upload. Backup is up to date!")
            return

        logging.info(f"Starting upload of {len(pending)} files...")
        
        batch = []
        conn = self._get_db_connection()
        cursor = conn.cursor()
        
        # Exponential backoff parameters
        backoff_time = 2.0
        max_backoff = 60.0
        
        try:
            for idx, (filepath, file_size) in enumerate(pending):
                logging.info(f"[{idx+1}/{len(pending)}] Processing: {filepath} ({file_size / (1024*1024):.2f} MB)")
                
                # Upload bytes
                token = None
                retries = 3
                while retries > 0:
                    token = self.upload_file_bytes(filepath, file_size)
                    if token:
                        break
                    
                    retries -= 1
                    if retries > 0:
                        logging.warning(f"Upload failed. Retrying in {backoff_time}s... ({retries} retries left)")
                        time.sleep(backoff_time)
                        backoff_time = min(backoff_time * 2, max_backoff)
                        self.refresh_session_token()
                
                if token:
                    batch.append((filepath, token))
                    # Reset backoff time on success
                    backoff_time = 2.0
                else:
                    cursor.execute(
                        "UPDATE uploads SET status = 'failed', error_message = 'Failed to upload file bytes' WHERE filepath = ?",
                        (filepath,)
                    )
                    conn.commit()
                    logging.error(f"Skipping {filepath} due to upload failure.")
                
                # If batch is full, commit it
                if len(batch) >= self.batch_size:
                    logging.info(f"Committing batch of {len(batch)} items to Google Photos...")
                    self.commit_batch(batch)
                    batch = []
                    
            # Commit any remaining items
            if batch:
                logging.info(f"Committing final batch of {len(batch)} items...")
                self.commit_batch(batch)
                
        except KeyboardInterrupt:
            logging.warning("Backup interrupted by user. Saving state and exiting...")
            if batch:
                logging.info(f"Committing partial batch of {len(batch)} items before exit...")
                try:
                    self.commit_batch(batch)
                except Exception as e:
                    logging.error(f"Failed to commit partial batch: {e}")
            sys.exit(0)
        finally:
            conn.close()
            logging.info("Backup run complete.")


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description="Backup local photos/videos recursively to Google Photos with resume support.")
    parser.add_argument("--src-dir", required=True, help="Source directory containing images/videos.")
    parser.add_argument("--db", default="backup_state.db", help="Path to SQLite state database file.")
    parser.add_argument("--credentials", default="credentials.json", help="Path to Google Client Secrets credentials.json.")
    parser.add_argument("--token", default="token.json", help="Path to saved Google token.json credentials.")
    parser.add_argument("--album", default=None, help="Google Photos album name to upload into.")
    parser.add_argument("--batch-size", type=int, default=50, help="Number of items to commit at once (max 50).")
    parser.add_argument("--types", default=".jpg,.jpeg,.png,.gif,.webp,.mp4,.mov", 
                        help="Comma-separated list of file extensions to upload.")
    
    args = parser.parse_args()
    
    extensions = [ext.strip().lower() for ext in args.types.split(",") if ext.strip()]
    if not all(ext.startswith(".") for ext in extensions):
        print("Error: Extensions in --types must start with a dot, e.g. .jpg,.png")
        sys.exit(1)
        
    if not os.path.isdir(args.src_dir):
        print(f"Error: Source directory does not exist: {args.src_dir}")
        sys.exit(1)

    backup_tool = GooglePhotosBackup(
        src_dir=args.src_dir,
        db_path=args.db,
        credentials_path=args.credentials,
        token_path=args.token,
        album_name=args.album,
        batch_size=args.batch_size
    )
    
    backup_tool.run(extensions)
