using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace BaseEntity.Toolkit.Sensitivity
{
    /// <summary>
    /// Array of Derivative collections to store sensitivities of an array of pricers
    /// </summary>
    public class DerivativeCollectionArray
    {
        private List<DerivativeCollection> collections_;
        private Dictionary<string, int> map_;
        private int index_;

        /// <summary>
        /// Create an array of DerivativeCollections corresponding to an array of pricers
        /// </summary>
        /// <param name="collections"></param>
        public DerivativeCollectionArray(DerivativeCollection[] collections)
        {
            index_ = 0;
            collections_ = new List<DerivativeCollection>(collections.Length);
            map_ = new Dictionary<string, int>(collections.Length);
            for(int i = 0; i < collections.Length; i++)
            {
                Add(collections[i]);
            }
        }

        /// <summary>
        /// Add a collection to the array
        /// </summary>
        /// <param name="collection">DerivativesCollection object</param>
        public void Add(DerivativeCollection collection)
        {
            try
            {
                map_.Add(collection.Name, index_);
            }
            catch(Exception)
            {
                throw new ArgumentException("{0} is not a valid name for the collection", collection.Name);
            }
            collections_.Add(collection);
            index_++;
        }

        /// <summary>
        /// Clear the collection
        /// </summary>
        public void Clear()
        {
            collections_.Clear();
            map_.Clear();
            index_ = 0;
        }

        /// <summary>
        /// Compute sensitivity to arbitrary (small) bumps in each tenor
        /// </summary>
        /// <param name="pricer">String identifying one of the pricers from which the collection was built</param>
        /// <param name="curve">String identifying the curve we want to bump</param>
        /// <param name="bumps">Bump size (must be of same size as curve tenor)</param>
        /// <returns>Computed sensitivity</returns>
        public double ScenarioSensitivity(string pricer, string curve, double[] bumps)
        {
            int pos = 0;
            double retVal = 0;
            try
            {
                pos = map_[pricer];
                
            }
            catch (Exception)
            {
                
                throw new ArgumentException("Pricer {0} not found in the collection", pricer);
            }
            try
            {
                retVal = collections_[pos].GetDerivatives(curve).ComputeSensitivity(bumps);

            }
            catch (Exception)
            {
                
                throw new ArgumentException("Curve {0} not found in the collection", curve);
            }
            return retVal;
        }

        /// <summary>
        /// Access the ith collection in the array
        /// </summary>
        /// <param name="i">Collection index</param>
        /// <returns>DerivativeCollection</returns>
        public DerivativeCollection GetCollection(int i)
        {
            return collections_[i];
        }

        /// <summary>
        /// Number of collections in the array
        /// </summary>
        public int Count
        {
            get
            {
                return collections_.Count;
            }
        }





    }
}
