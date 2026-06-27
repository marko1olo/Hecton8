//System
using System;
using System.Collections;
using System.Collections.Generic;
//Unity
using UnityEngine;
using UnityEngine.UI;
//Candice AI
using CandiceAIforGames.AI;

namespace CandiceAIforGames.AI
{

    public class CandiceHealthBar : MonoBehaviour
    {
		[Header("Colors")]
		[SerializeField] private Color m_MainColor = Color.white;
		[SerializeField] private Color m_FillColor = Color.green;

		[Header("General")]
		[SerializeField] private int m_NumberOfSegments = 5;
		[SerializeField] private float m_SizeOfNotch = 5;
		[Range(0, 1f)] [SerializeField] [UnityEngine.Serialization.FormerlySerializedAs("m_FillAmount")]
		private float _fillAmount = 0.0f;

		public float m_FillAmount
		{
			get { return _fillAmount; }
			set
			{
				if (_fillAmount != value)
				{
					_fillAmount = value;
					UpdateSegments();
				}
			}
		}

		private RectTransform m_RectTransform;
		private Image m_Image;
		private List<Image> m_ProgressToFill = new List<Image>();
		private float m_SizeOfSegment;

		public void Awake()
		{
			// get rect transform
			m_RectTransform = (RectTransform)transform;

			// get image
			m_Image = GetComponentInChildren<Image>();
			m_Image.color = m_MainColor;
			m_Image.gameObject.SetActive(false);

			// count size of segments
			m_SizeOfSegment = m_RectTransform.sizeDelta.x / m_NumberOfSegments;
			Vector3 right = Vector3.right;
			Vector3 posOffsetBase = right * m_SizeOfSegment * (m_NumberOfSegments / 2);
			Vector3 startPos = transform.position;
			Quaternion rot = Quaternion.identity;

			for (int i = 0; i < m_NumberOfSegments; i++)
			{
				Image segmentImage = Instantiate(m_Image, startPos, rot, transform);
				segmentImage.gameObject.SetActive(true);

				segmentImage.fillAmount = m_SizeOfSegment;

				RectTransform segmentRectTransform = segmentImage.rectTransform;
				segmentRectTransform.sizeDelta = new Vector2(m_SizeOfSegment, segmentRectTransform.sizeDelta.y);
				segmentRectTransform.position += (right * i * m_SizeOfSegment) - posOffsetBase + (right * i * m_SizeOfNotch);

				Transform childTransform = segmentImage.transform.GetChild(0);
				if (childTransform.TryGetComponent<Image>(out Image segmentFillImage))
				{
					segmentFillImage.color = m_FillColor;
					m_ProgressToFill.Add(segmentFillImage);

					RectTransform segmentFillRectTransform = segmentFillImage.rectTransform;
					segmentFillRectTransform.sizeDelta = new Vector2(m_SizeOfSegment, segmentFillRectTransform.sizeDelta.y);
				}
			}

			UpdateSegments();
		}

		private void UpdateSegments()
		{
			int count = m_ProgressToFill.Count;
			for (int i = 0; i < m_NumberOfSegments; i++)
			{
				if (i < count)
				{
					m_ProgressToFill[i].fillAmount = m_NumberOfSegments * _fillAmount - i;
				}
			}
		}

		private float ConvertFragmentToWidth(float fragment)
		{
			return m_RectTransform.sizeDelta.x * fragment;
		}
	}
}
