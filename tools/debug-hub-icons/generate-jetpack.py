"""本任务的生图入口：复用指定技能，兼容当前 provider 已配置的 bearer 凭据。"""
import importlib.util
import json
from pathlib import Path
import tomllib


skill = Path.home() / ".codex/skills/imagegen-codex-provider/scripts/run_imagegen.py"
spec = importlib.util.spec_from_file_location("configured_imagegen", skill)
adapter = importlib.util.module_from_spec(spec)
spec.loader.exec_module(adapter)


def connection(provider_override):
    task_home = adapter.codex_home()
    with (task_home / "config.toml").open("rb") as stream:
        config = tomllib.load(stream)
    provider_name = provider_override or config["model_provider"]
    provider = config["model_providers"][provider_name]
    auth = json.loads((task_home / "auth.json").read_text(encoding="utf-8"))
    credential = auth.get("OPENAI_API_KEY") or provider.get("experimental_bearer_token")
    if not isinstance(credential, str) or not credential:
        adapter.fail("Configured provider has no usable credential")
    # 仅在进程内传给原适配器；不写认证文件、不输出凭据、不改变 provider。
    print("provider:", provider_name, flush=True)
    print("base_url:", provider["base_url"], flush=True)
    return task_home, provider_name, provider["base_url"].rstrip("/"), credential


adapter.load_connection = connection
raise SystemExit(adapter.main())
