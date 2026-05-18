/**
 * 公开监控摄像头参考数据
 * 
 * 说明：以下为可以查到的公共监控/直播流地址和坐标，用于测试视频类型点位。
 * 这些地址可能随时间变化，建议在实际使用前验证可用性。
 * 
 * 使用示例 - 可作为 GisGeometry 的视频类型点位数据导入：
 *   Type = Video (5)
 *   Att  = 视频流地址
 *   Gps  = 经纬度
 *   Name = 摄像头名称
 * 
 * 常用公开视频流地址（测试用）：
 * 
 * 1. 国内公共监控（需要自行查找当地政府公开的监控点）
 * 
 * 2. 国际公共直播/流媒体测试地址：
 *    - https://test-streams.mux.dev/x36xhzz/x36xhzz.m3u8       (Mux HLS 测试流)
 *    - https://bitdash-a.akamaihd.net/content/sintel/hls/playlist.m3u8 (Akamai HLS)
 *    - http://commondatastorage.googleapis.com/gtv-videos-bucket/sample/BigBuckBunny.mp4 (Google 测试视频)
 * 
 * 3. 公开 RTSP 地址（一般只能内网查看）：
 *    公共 RTSP 流大多已失效，建议使用 HLS 流
 */
window.GisPublicCameras = [
    {
        name: 'Mux HLS 测试流',
        url: 'https://test-streams.mux.dev/x36xhzz/x36xhzz.m3u8',
        gps: '120.6034,27.5686',
        addr: '测试地址'
    },
    {
        name: 'Big Buck Bunny 测试视频',
        url: 'http://commondatastorage.googleapis.com/gtv-videos-bucket/sample/BigBuckBunny.mp4',
        gps: '120.6100,27.5600',
        addr: '测试地址'
    },
    {
        name: 'Sintel HLS 测试流',
        url: 'https://bitdash-a.akamaihd.net/content/sintel/hls/playlist.m3u8',
        gps: '120.6200,27.5750',
        addr: '测试地址'
    }
];
