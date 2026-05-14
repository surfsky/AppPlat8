mkdir -p App/Files/Samples
urls=(
"https://raw.githubusercontent.com/KhronosGroup/glTF-Sample-Models/master/2.0/Sponza/glTF-Binary/Sponza.glb"
"https://raw.githubusercontent.com/KhronosGroup/glTF-Sample-Assets/main/Models/Sponza/glTF-Binary/Sponza.glb"
"https://raw.githubusercontent.com/KhronosGroup/glTF-Sample-Assets/main/Models/LittlestTokyo/glTF-Binary/LittlestTokyo.glb"
)
success=0
for url in "${urls[@]}"; do
  echo "Trying $url..."
  if curl -L -f -s -o App/Files/Samples/building-sample.glb "$url"; then
    echo "Successfully downloaded from $url"
    success=1
    used_url=$url
    break
  else
    echo "Failed to download from $url (HTTP status: $(curl -L -I -s "$url" | head -n 1))"
  fi
done
if [ $success -eq 1 ]; then
  ls -lh App/Files/Samples/building-sample.glb
  file App/Files/Samples/building-sample.glb
  echo "Final URL: $used_url"
else
  echo "All downloads failed."
  exit 1
fi
