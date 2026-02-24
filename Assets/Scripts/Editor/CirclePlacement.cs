using UnityEngine;

public class CirclePlacement : MonoBehaviour
{
    [Tooltip("Массив объектов для расстановки")]
    public Transform[] objectsToPlace;
    
    [Tooltip("Радиус окружности")]
    public float radius = 0.28f;

    // OnValidate вызывается при изменении любого поля в инспекторе
    private void OnValidate()
    {
        PlaceObjects();
    }

    private void PlaceObjects()
    {
        if (objectsToPlace == null || objectsToPlace.Length == 0) return;

        int count = objectsToPlace.Length;
        
        // Проходим по всем объектам в массиве
        for (int i = 0; i < count; i++)
        {
            if (objectsToPlace[i] == null) continue;

            // Вычисляем угол для текущего объекта в радианах
            // (2 * PI / количество объектов) * индекс объекта
            float angle = i * Mathf.PI * 2f / count;

            // Вычисляем координаты X и Y
            float x = Mathf.Cos(angle) * radius;
            float y = Mathf.Sin(angle) * radius;

            // Устанавливаем локальную позицию (относительно родителя)
            // Используем X и Z для горизонтальной плоскости, либо X и Y для 2D
            objectsToPlace[i].localPosition = new Vector3(x, y, x); // Для 3D можно использовать (x, 0, y) или (x, y, 0) в зависимости от ориентации
            
            // Опционально: разворачиваем объекты «лицом» от центра
           // objectsToPlace[i].LookAt(transform.position + objectsToPlace[i].localPosition * 2f);
        }
    }

    // Отрисовка круга в редакторе для наглядности
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.black;
        Gizmos.DrawWireSphere(transform.position, radius);
    }
}