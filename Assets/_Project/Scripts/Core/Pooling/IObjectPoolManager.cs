using UnityEngine;

namespace GameYunit.Core.Pooling
{
    /// <summary>
    /// Serviço central de pooling. Todo objeto que spawna em massa (inimigos, projéteis, XP, VFX)
    /// deve passar por aqui em vez de usar Instantiate/Destroy diretamente.
    /// </summary>
    /// <remarks>
    /// Consumidores devem depender desta interface (não da implementação concreta), o que permite
    /// trocar a implementação — por exemplo, por uma versão integrada ao Netcode — sem tocar em quem usa.
    /// </remarks>
    public interface IObjectPoolManager
    {
        /// <summary>
        /// Garante ao menos <paramref name="count"/> instâncias inativas prontas para o prefab.
        /// Chame em telas de loading para evitar picos de Instantiate durante o gameplay.
        /// </summary>
        void Prewarm(GameObject prefab, int count);

        /// <summary>
        /// Retira uma instância do pool (criando se necessário), já posicionada e ativa.
        /// </summary>
        /// <param name="prefab">Prefab de origem; também é a chave do pool.</param>
        /// <param name="position">Posição em world space.</param>
        /// <param name="rotation">Rotação em world space.</param>
        /// <param name="parent">
        /// Parent opcional. Prefira null para objetos móveis em massa: objetos na raiz da cena
        /// têm o update de Transform paralelizado pela Unity.
        /// </param>
        GameObject Get(GameObject prefab, Vector3 position, Quaternion rotation, Transform parent = null);

        /// <summary>
        /// Versão tipada de <see cref="Get(GameObject, Vector3, Quaternion, Transform)"/>.
        /// O componente <typeparamref name="T"/> deve estar na raiz do prefab.
        /// </summary>
        T Get<T>(T prefab, Vector3 position, Quaternion rotation, Transform parent = null) where T : Component;

        /// <summary>
        /// Devolve a instância ao pool. Objetos que não vieram de um pool são destruídos com aviso.
        /// </summary>
        void Release(GameObject instance);

        /// <summary>
        /// Descarta o pool de um prefab, destruindo as instâncias inativas.
        /// Instâncias ativas são destruídas quando forem devolvidas.
        /// </summary>
        void ClearPool(GameObject prefab);

        /// <summary>
        /// Descarta todos os pools (ex.: ao encerrar uma partida).
        /// </summary>
        void ClearAll();
    }
}
