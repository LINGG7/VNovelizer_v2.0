using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class ImageFlicker : MonoBehaviour
{
    public Image uiImage;

    [Header("时间设置")]
    public float minNormalTime = 0.5f;
    public float maxNormalTime = 1.5f;

    [Header("电火花特效")]
    public ParticleSystem sparkParticles;
    public float sparkXRange = 150f;       // X轴随机偏移的范围（根据UI大小调整）

    private Vector3 sparkInitialLocalPos; // 记录粒子的初始本地位置


    void Start()
    {
        if (uiImage == null) uiImage = GetComponent<Image>();

        // 记录粒子系统的初始位置，防止它越飘越远
        if (sparkParticles != null)
        {
            sparkInitialLocalPos = sparkParticles.transform.localPosition;
        }

        StartCoroutine(FlickerRoutine());
    }

    IEnumerator FlickerRoutine()
    {
        while (true)
        {
            SetAlpha(1f);
            yield return new WaitForSeconds(Random.Range(minNormalTime, maxNormalTime));

            // --- 抽搐闪烁阶段 ---
            int flickerCount = Random.Range(2, 6);

            // 在播放火花前，随机设置它的 X 轴位置
            if (sparkParticles != null)
            {
                // 计算随机偏移
                float randomOffset = Random.Range(-sparkXRange, sparkXRange);

                // 应用偏移（只改变X，保持Y和Z不变）
                sparkParticles.transform.localPosition = new Vector3(
                    sparkInitialLocalPos.x + randomOffset,
                    sparkInitialLocalPos.y,
                    sparkInitialLocalPos.z
                );

                sparkParticles.Play();
            }

            for (int i = 0; i < flickerCount; i++)
            {
                SetAlpha(0.2f);
                yield return new WaitForSeconds(Random.Range(0.02f, 0.08f));
                SetAlpha(0.8f);
                yield return new WaitForSeconds(Random.Range(0.02f, 0.08f));
            }

            if (Random.value > 0.7f)
            {
                SetAlpha(0.1f);
                yield return new WaitForSeconds(Random.Range(0.2f, 0.6f));
            }
        }
    }

    void SetAlpha(float alpha)
    {
        if (uiImage != null)
        {
            Color c = uiImage.color;
            c.a = alpha;
            uiImage.color = c;
        }
    }
}