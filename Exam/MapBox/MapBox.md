创建
    mapboxgl.accessToken = 'YOUR_MAPBOX_TOKEN';
    const map = new mapboxgl.Map({
      container: 'map',
      style: 'mapbox://styles/mapbox/satellite-v9', // 卫星底图
      center: [116.4, 39.9], // 默认北京
      zoom: 5,
      projection: 'globe'
    });


控件
    map.addControl(new mapboxgl.NavigationControl(), 'top-right');  // 导航
    map.addControl(new mapboxgl.GeolocateControl({
        positionOptions: { enableHighAccuracy: true },
        trackUserLocation: true,
        showUserHeading: true
    }), 'top-right');  // 定位
    map.addControl(new mapboxgl.AttributionControl({ compact: true }), 'bottom-right');  // 版权信息
    map.addControl(new mapboxgl.ScaleControl({ unit: 'metric' }), 'bottom-left');  // 比例尺

    map.addControl(new mapboxgl.FullscreenControl());  // 全屏
    map.addControl(new mapboxgl.FullscreenControl({ container: document.documentElement }), 'top-right');

标注（marker）
    const marker = new mapboxgl.Marker();
    marker.setLngLat([116.4, 39.9]);
    marker.addTo(map);

文本
    const text = new mapboxgl.Text();
    text.setLngLat([116.4, 39.9]);
    text.addTo(map);
    text.setText('北京');