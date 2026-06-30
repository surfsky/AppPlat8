using System;
using System.Collections.Generic;
using System.Linq;

namespace App.Pages;

/// <summary>
/// 滑动验证码数据模型
/// </summary>
public class SliderData
{
    public int Duration { get; set; }
    public List<Point> Points { get; set; }
}

/// <summary>
/// 滑动验证码轨迹点模型
/// </summary>
public class Point
{
    public double X { get; set; }
    public double Y { get; set; }
    public long T { get; set; }
}

public class SliderVerifier
{
    /// <summary>
    /// 校验滑动验证码轨迹数据是否符合人类操作。包含这些检测点（命中即拒绝）：
    /// - 速度/时长边界 ： Duration 过快（<350ms）或超时（>15s）
    /// - 数据完整性 ：点数太少/太多；时间戳不递增
    /// - 范围合法性 ： x/y 必须在 [-1.2, 1.2] ，并且起点接近 -1 、终点接近 1
    /// - 单调性 ：允许少量回拉，但回拉次数/回拉总量超过阈值直接拒绝
    /// - Y 轴“抖动” ：必须有合理浮动（防止 y 恒定的机器人轨迹）
    /// - 加速-减速特征 ：计算速度序列，峰值位置不能太靠前/太靠后，且峰值要明显高于均值
    /// - 过于精密（机器人） ：
    /// - dt 和 dx 的变异系数同时过小（步长/间隔过“齐”）
    /// - x(t) 近似完美线性（平均误差过小 + dt 稳定），判定“轨迹过于精密”
    /// </summary>
    public static (bool ok, string msg) Validate(SliderData data)
    {
        if (data == null) 
            return (false, "数据异常");
        if (data.Duration < 350)
            return (false, "太快了");
        if (data.Duration > 15000)
            return (false, "超时");

        var pts = data.Points ?? new List<Point>();
        if (pts.Count < 8)
            return (false, "轨迹异常");
        if (pts.Count > 200)
            return (false, "轨迹异常");

        pts = pts.OrderBy(t => t.T).ToList();
        if (pts[0].T < 0)
            return (false, "轨迹异常");

        var xs = pts.Select(p => p.X).ToList();
        var ys = pts.Select(p => p.Y).ToList();

        if (xs.Any(x => x < -1.2 || x > 1.2) || ys.Any(y => y < -1.2 || y > 1.2))
            return (false, "轨迹异常");

        if (xs[0] > -0.8)
            return (false, "轨迹异常");
        if (xs[^1] < 0.8)
            return (false, "轨迹异常");

        var dtList = new List<double>();
        var dxList = new List<double>();
        int backSteps = 0;
        double backSum = 0;

        for (int i = 1; i < pts.Count; i++)
        {
            var dt = pts[i].T - pts[i - 1].T;
            if (dt <= 0)
                return (false, "轨迹异常");
            dtList.Add(dt);

            var dx = xs[i] - xs[i - 1];
            dxList.Add(dx);

            if (dx < -0.03)
            {
                backSteps++;
                backSum += -dx;
            }
        }

        if (backSteps > 2 || backSum > 0.18)
            return (false, "轨迹异常");

        var yRange = ys.Max() - ys.Min();
        var yStd = StdDev(ys);
        if (yRange < 0.03 || yStd < 0.01)
            return (false, "轨迹异常");
        if (yRange > 1.2)
            return (false, "轨迹异常");

        var posDx = dxList.Where(x => x > 0.0001).ToList();
        if (posDx.Count < 5)
            return (false, "轨迹异常");

        var cvDt = CoefVar(dtList);
        var cvDx = CoefVar(posDx);
        if (pts.Count >= 15 && cvDt < 0.08 && cvDx < 0.10)
            return (false, "轨迹过于精密");

        var velocities = new List<double>();
        for (int i = 0; i < dxList.Count; i++)
        {
            var dx = dxList[i];
            var dt = dtList[i];
            if (dt <= 0)
                continue;
            velocities.Add(dx / dt);
        }

        var posV = velocities.Where(v => v > 0).ToList();
        if (posV.Count < 5)
            return (false, "轨迹异常");

        var peak = posV.Max();
        var meanV = posV.Average();
        if (meanV <= 0)
            return (false, "轨迹异常");

        var peakIndex = 0;
        var peakVal = double.MinValue;
        for (int i = 0; i < velocities.Count; i++)
        {
            if (velocities[i] > peakVal)
            {
                peakVal = velocities[i];
                peakIndex = i;
            }
        }
        var minPeakIndex = (int)Math.Floor(velocities.Count * 0.20);
        var maxPeakIndex = (int)Math.Ceiling(velocities.Count * 0.90);
        if (peakIndex < minPeakIndex || peakIndex > maxPeakIndex)
            return (false, "轨迹异常");

        if (peak < meanV * 1.15)
            return (false, "轨迹异常");

        var linearErr = MeanAbsLinearFitError(pts.Select(p => (double)p.T).ToList(), xs);
        if (pts.Count >= 18 && linearErr < 0.004 && cvDt < 0.12)
            return (false, "轨迹过于精密");

        return (true, "");
    }

    private static double MeanAbsLinearFitError(List<double> t, List<double> x)
    {
        if (t == null || x == null || t.Count != x.Count || t.Count < 3)
            return 1;

        var t0 = t[0];
        var t1 = t[^1];
        var denom = (t1 - t0);
        if (denom <= 0)
            return 1;

        double sumAbs = 0;
        for (int i = 0; i < t.Count; i++)
        {
            var u = (t[i] - t0) / denom;
            if (u < 0) u = 0;
            if (u > 1) u = 1;
            var pred = -1 + 2 * u;
            sumAbs += Math.Abs(x[i] - pred);
        }
        return sumAbs / t.Count;
    }

    private static double StdDev(List<double> values)
    {
        if (values == null || values.Count == 0)
            return 0;
        var mean = values.Average();
        var sum = 0.0;
        for (int i = 0; i < values.Count; i++)
            sum += (values[i] - mean) * (values[i] - mean);
        return Math.Sqrt(sum / values.Count);
    }

    private static double CoefVar(List<double> values)
    {
        if (values == null || values.Count == 0)
            return 1;
        var mean = values.Average();
        if (mean <= 0)
            return 1;
        var std = StdDev(values);
        return std / mean;
    }

}