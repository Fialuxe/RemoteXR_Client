# RemoteXR_Client


## set up eye and face mesh tracking
### Mac
if you are using mac or equivalent(zsh or bash?),
you can use run_demo.sh to run the demo to track eye-gaze and face mesh.
```
cd /path/to/remoteXR_client
./run_demo.sh
```
### Windows
if you are using windows, please install python and run the command below.

```
cd \path\to\remoteXR_client
python -m venv .\venv
.\venv\Scripts\activate
pip install requirements.txt
python lsl_server.py
```

Thank you.

https://github.com/ck-zhang/EyeTrax