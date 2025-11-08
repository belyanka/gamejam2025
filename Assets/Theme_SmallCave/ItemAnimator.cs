using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

public class ItemAnimator : MonoBehaviour
{
    private Sequence sequence;
    public Transform visualTransform;
    
    public void WarningShake()
    {
        sequence?.Kill();
        sequence = DOTween.Sequence();

        sequence.Append(visualTransform.DOShakePosition(3f,0.05f,20,90f,false,false,ShakeRandomnessMode.Full));
        
        sequence.Play();
    }

    public void StopShake()
    {
        sequence?.Kill();
        sequence = DOTween.Sequence();

        sequence.Append(visualTransform.DOLocalMove(Vector3.zero, 0.2f));
        
        sequence.Play();
    }
}
