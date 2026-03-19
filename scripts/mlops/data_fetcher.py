import requests
import os
from urllib3.util.retry import Retry
from requests.adapters import HTTPAdapter
from config import logger, DATASET_EXPORT_API, API_KEY

def get_session():
    session = requests.Session()
    retry = Retry(
        total=5,
        backoff_factor=1,
        status_forcelist=[500, 502, 503, 504]
    )
    adapter = HTTPAdapter(max_retries=retry)
    session.mount("http://", adapter)
    session.mount("https://", adapter)
    return session

import urllib3
urllib3.disable_warnings(urllib3.exceptions.InsecureRequestWarning)

def fetch_verified_data(since=None):
    logger.info(f"Fetching verified data from: {DATASET_EXPORT_API}")
    headers = {"X-Api-Key": f"{API_KEY}"}
    params = {"since": since} if since else {}
    
    session = get_session()
    try:
        response = session.get(DATASET_EXPORT_API, headers=headers, params=params, timeout=30, verify=False)
        response.raise_for_status()
        data = response.json()
        logger.info(f"Successfully fetched {data['data']['totalSamples']} samples.")
        return data['data']['samples']
    except requests.exceptions.RequestException as e:
        logger.error(f"API request failed: {e}")
        return []

def download_image(url, destination_path):
    if os.path.exists(destination_path):
        return True
        
    session = get_session()
    try:
        r = session.get(url, timeout=30)
        r.raise_for_status()
        with open(destination_path, "wb") as f:
            f.write(r.content)
        return True
    except Exception as e:
        logger.error(f"Failed to download {url}: {e}")
        return False