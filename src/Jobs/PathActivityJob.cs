using Colossal;
using Game.Citizens;
using Game.Common;
using Game.Creatures;
using Game.Net;
using Game.Pathfind;
using Game.Vehicles;
using Game.Buildings;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using BetterTransitView.Systems;

namespace BetterTransitView.Jobs
{
    [BurstCompile]
    public partial struct PathActivityJob : IJobEntity
    {
        [ReadOnly] public NativeHashSet<Entity> targets;
        [ReadOnly] public bool checkPathElements;

        // Citizen Lookups
        [ReadOnly] public ComponentLookup<TravelPurpose> travelPurposeLookup;
        [ReadOnly] public ComponentLookup<Target> targetLookup;
        [ReadOnly] public ComponentLookup<Household> householdLookup;
        [ReadOnly] public ComponentLookup<HouseholdMember> householdMemberLookup;
        [ReadOnly] public ComponentLookup<Worker> workerLookup;
        [ReadOnly] public ComponentLookup<Game.Citizens.Student> studentLookup;
        [ReadOnly] public ComponentLookup<Game.Creatures.Resident> creatureResidentLookup;
        [ReadOnly] public ComponentLookup<TouristHousehold> touristHouseholdLookup; 
        [ReadOnly] public ComponentLookup<PropertyRenter> propertyRenterLookup;
        [ReadOnly] public ComponentLookup<Owner> ownerLookup;
        [ReadOnly] public ComponentLookup<Building> buildingLookup;
        [ReadOnly] public ComponentLookup<CurrentVehicle> currentVehicleLookup;
        [ReadOnly] public ComponentLookup<CurrentTransport> currentTransportLookup;

        // Vehicle Lookups
        [ReadOnly] public ComponentLookup<PersonalCar> personalCarLookup;
        [ReadOnly] public ComponentLookup<DeliveryTruck> deliveryTruckLookup;
        [ReadOnly] public ComponentLookup<CargoTransport> cargoTransportLookup;
        [ReadOnly] public ComponentLookup<PublicTransport> publicTransportLookup;
        
        // Taxi Lookups
        [ReadOnly] public ComponentLookup<Game.Vehicles.Taxi> taxiLookup;
        [ReadOnly] public BufferLookup<Passenger> passengerLookup;

        // Service Vehicle Lookups
        [ReadOnly] public ComponentLookup<Game.Vehicles.Hearse> hearseLookup;
        [ReadOnly] public ComponentLookup<Game.Vehicles.GarbageTruck> garbageTruckLookup;
        [ReadOnly] public ComponentLookup<Game.Vehicles.PoliceCar> policeCarLookup;
        [ReadOnly] public ComponentLookup<Game.Vehicles.FireEngine> fireEngineLookup;
        [ReadOnly] public ComponentLookup<Game.Vehicles.Ambulance> ambulanceLookup;
        [ReadOnly] public ComponentLookup<Game.Vehicles.PostVan> postVanLookup;
        [ReadOnly] public ComponentLookup<Game.Vehicles.MaintenanceVehicle> maintenanceVehicleLookup;
        
        // For on selected segment
        [ReadOnly] public ComponentLookup<CarCurrentLane> carLaneLookup;
        [ReadOnly] public ComponentLookup<HumanCurrentLane> humanLaneLookup;
        [ReadOnly] public ComponentLookup<Game.Vehicles.Vehicle> vehicleLookup;
        [ReadOnly] public ComponentLookup<TrainCurrentLane> trainLaneLookup;
        [ReadOnly] public ComponentLookup<WatercraftCurrentLane> watercraftLaneLookup;
        [ReadOnly] public BufferLookup<CarNavigationLane> carNavigationLaneLookup;

        public NativeQueue<TrafficRenderData>.ParallelWriter results;

        public void Execute(Entity entity, DynamicBuffer<PathElement> path)
        {
            bool passesThrough = false;
            
            // Check Path Buffer (Future macro-path)
            if (checkPathElements)
            {
                for (int i = 0; i < path.Length; i++)
                {
                    if (IsTargetMatch(path[i].m_Target))
                    {
                        passesThrough = true;
                        break;
                    }
                }
            }

            // Check Navigation Lane Buffer (Immediate short-term future path)
            if (!passesThrough && carNavigationLaneLookup.TryGetBuffer(entity, out DynamicBuffer<CarNavigationLane> navLanes))
            {
                for (int i = 0; i < navLanes.Length; i++)
                {
                    if (IsTargetMatch(navLanes[i].m_Lane))
                    {
                        passesThrough = true;
                        break;
                    }
                }
            }

            // Check Current Physical Position
            if (!passesThrough)
            {
                if (carLaneLookup.TryGetComponent(entity, out CarCurrentLane carLane))
                {
                    if (targets.Contains(carLane.m_Lane)) passesThrough = true;
                }
                else if (humanLaneLookup.TryGetComponent(entity, out HumanCurrentLane humanLane))
                {
                    if (targets.Contains(humanLane.m_Lane)) passesThrough = true;
                }
                else if (trainLaneLookup.TryGetComponent(entity, out Game.Vehicles.TrainCurrentLane trainLane))
                {
                    if (targets.Contains(trainLane.m_Front.m_Lane) || targets.Contains(trainLane.m_Rear.m_Lane)) 
                    {
                        passesThrough = true;
                    }
                }
                else if (watercraftLaneLookup.TryGetComponent(entity, out WatercraftCurrentLane watercraftLane))
                {
                    if (targets.Contains(watercraftLane.m_Lane)) passesThrough = true;
                }
            }

            if (!passesThrough) return;
            
            if (vehicleLookup.HasComponent(entity))
            {
                if (!AnalyzeVehicle(entity))
                {
                    EnqueueVehicleDestination(entity, Purpose.None, TrafficType.Service, false, false);
                }
                return;
            }

            AnalyzeCitizen(entity);
        }

        // HELPER METHOD: Climbs up to 2 levels of parents to catch micro-pathing entities
        private bool IsTargetMatch(Entity pathTarget)
        {
            // Direct Match (Edges, Nodes, SubLanes)
            if (targets.Contains(pathTarget)) return true;
            
            // 1st Level Parent Check (e.g. Lane -> SubLane)
            if (ownerLookup.TryGetComponent(pathTarget, out Owner owner1))
            {
                if (targets.Contains(owner1.m_Owner)) return true;
                
                // 2nd Level Parent Check (e.g. SubLane -> Edge)
                if (ownerLookup.TryGetComponent(owner1.m_Owner, out Owner owner2))
                {
                    if (targets.Contains(owner2.m_Owner)) return true;
                }
            }
            
            return false;
        }

        private bool AnalyzeVehicle(Entity entity)
        {
            // 1. Service Vehicles
            if (hearseLookup.HasComponent(entity) ||
                garbageTruckLookup.HasComponent(entity) ||
                policeCarLookup.HasComponent(entity) ||
                fireEngineLookup.HasComponent(entity) ||
                ambulanceLookup.HasComponent(entity) ||
                postVanLookup.HasComponent(entity) ||
                maintenanceVehicleLookup.HasComponent(entity))
            {
                EnqueueVehicleDestination(entity, Purpose.None, TrafficType.Service, false, false);
                return true;
            }

            // Taxis
            if (taxiLookup.HasComponent(entity))
            {
                Purpose passengerPurpose = Purpose.None;
                TrafficType type = TrafficType.Service; 
                bool isMovingIn = false;
                bool isTourist = false;

                // Try to find a passenger to get the real purpose
                if (passengerLookup.TryGetBuffer(entity, out DynamicBuffer<Passenger> passengers) && passengers.Length > 0)
                {
                    for (int i = 0; i < passengers.Length; i++)
                    {
                        Entity passenger = passengers[i].m_Passenger;
                        if (travelPurposeLookup.TryGetComponent(passenger, out TravelPurpose purpose))
                        {
                            passengerPurpose = purpose.m_Purpose;
                            type = TrafficType.Citizen; // Upgrade to Citizen so it counts towards Shopping/Home/etc stats
                            
                            if (passengerPurpose == Purpose.GoingHome)
                            {
                                if (householdMemberLookup.TryGetComponent(passenger, out HouseholdMember householdMember) &&
                                    householdLookup.TryGetComponent(householdMember.m_Household, out Game.Citizens.Household household) &&
                                    (household.m_Flags & HouseholdFlags.MovedIn) == 0)
                                {
                                    isMovingIn = true;
                                }
                            }
                            
                            if (IsTourist(passenger)) isTourist = true;
                            
                            break; // Use the first valid passenger's purpose
                        }
                    }
                }

                EnqueueVehicleDestination(entity, passengerPurpose, type, isMovingIn, isTourist);
                return true;
            }

            // 2. Public Transport
            if (publicTransportLookup.HasComponent(entity))
            {
                EnqueueVehicleDestination(entity, Purpose.None, TrafficType.PublicTransport, false, false);
                return true;
            }

            // 3. Delivery / Cargo
            if (deliveryTruckLookup.TryGetComponent(entity, out DeliveryTruck truck))
            {
                Purpose p = (truck.m_State & DeliveryTruckFlags.Returning) != 0 ? Purpose.None : Purpose.Delivery;
                EnqueueVehicleDestination(entity, p, TrafficType.Cargo, false, false);
                return true;
            }

            // 4. Cargo
            if (cargoTransportLookup.TryGetComponent(entity, out CargoTransport cargo))
            {
                Purpose p = (cargo.m_State & CargoTransportFlags.Returning) != 0 ? Purpose.None : Purpose.Delivery;
                EnqueueVehicleDestination(entity, p, TrafficType.Cargo, false, false);
                return true;
            }

            // 5. Personal Cars
            if (personalCarLookup.TryGetComponent(entity, out PersonalCar car))
            {
                Purpose driverPurpose = Purpose.None;
                bool isMovingIn = false;
                bool isTourist = false;

                if (car.m_Keeper != Entity.Null)
                {
                    if (travelPurposeLookup.TryGetComponent(car.m_Keeper, out TravelPurpose purpose))
                    {
                        driverPurpose = purpose.m_Purpose;
                        if (driverPurpose == Purpose.GoingHome)
                        {
                            if (householdMemberLookup.TryGetComponent(car.m_Keeper, out HouseholdMember householdMember) &&
                                householdLookup.TryGetComponent(householdMember.m_Household, out Game.Citizens.Household household) &&
                                (household.m_Flags & HouseholdFlags.MovedIn) == 0)
                            {
                                isMovingIn = true;
                            }
                        }
                    }
                    
                    // Check if driver is tourist
                    if (IsTourist(car.m_Keeper)) isTourist = true;
                }
                EnqueueVehicleDestination(entity, driverPurpose, TrafficType.Citizen, isMovingIn, isTourist);
                return true;
            }

            return false;
        }

        private void AnalyzeCitizen(Entity entity)
        {
            // 1. Check if they are driving (CurrentVehicle) OR riding Public Transport (CurrentTransport)
            // If they have either, they are not "Pedestrians" on the road surface, so we skip them.
            if (currentVehicleLookup.HasComponent(entity) || currentTransportLookup.HasComponent(entity)) return;

            Entity citizenEntity = entity;
            if (creatureResidentLookup.TryGetComponent(entity, out Game.Creatures.Resident resident))
            {
                citizenEntity = resident.m_Citizen;
            }

            Purpose currentPurpose = Purpose.None;
            bool isMovingIn = false;
            bool isTourist = IsTourist(citizenEntity); // Check citizen entity

            if (travelPurposeLookup.TryGetComponent(citizenEntity, out TravelPurpose purpose))
            {
                currentPurpose = purpose.m_Purpose;
                if (currentPurpose == Purpose.GoingHome)
                {
                    if (householdMemberLookup.TryGetComponent(entity, out HouseholdMember householdMember) &&
                        householdLookup.TryGetComponent(householdMember.m_Household, out Game.Citizens.Household household) &&
                        (household.m_Flags & HouseholdFlags.MovedIn) == 0)
                    {
                        isMovingIn = true;
                    }
                }
            }

            EnqueueDestination(entity, currentPurpose, isMovingIn, isTourist);
        }

        private void EnqueueVehicleDestination(Entity vehicleEntity, Purpose purpose, TrafficType type, bool isMovingIn, bool isTourist)
        {
            Entity physicalDest = Entity.Null;
            if (targetLookup.TryGetComponent(vehicleEntity, out Target dest) && dest.m_Target != Entity.Null)
            {
                physicalDest = ResolvePhysicalEntity(dest.m_Target);
                if (targets.Contains(physicalDest)) physicalDest = Entity.Null;
            }

            results.Enqueue(new TrafficRenderData
            {
                entity = vehicleEntity,
                sourceAgent = vehicleEntity, // self
                destinationEntity = physicalDest, // Combined into one item
                purpose = purpose,
                type = type,
                isOrigin = false,
                isVehicle = true,
                isPedestrian = false,
                isDestination = false,
                isMovingIn = isMovingIn,
                isTourist = isTourist
            });
        }

        private void EnqueueDestination(Entity entity, Purpose purpose, bool isMovingIn, bool isTourist)
        {
            Entity physicalDest = Entity.Null;
            if (targetLookup.TryGetComponent(entity, out Target dest) && dest.m_Target != Entity.Null)
            {
                physicalDest = ResolvePhysicalEntity(dest.m_Target);
                if (targets.Contains(physicalDest)) physicalDest = Entity.Null;
            }

            results.Enqueue(new TrafficRenderData
            {
                entity = entity,
                sourceAgent = entity, 
                destinationEntity = physicalDest, // Combined into one item
                purpose = purpose,
                type = TrafficType.Citizen,
                isOrigin = false,
                isVehicle = false,
                isPedestrian = true,
                isDestination = false,
                isMovingIn = isMovingIn,
                isTourist = isTourist
            });
        }

        private Entity ResolvePhysicalEntity(Entity target)
        {
            if (target == Entity.Null) return Entity.Null;
            Entity current = target;
            if (propertyRenterLookup.TryGetComponent(current, out PropertyRenter renter))
                current = renter.m_Property;
            if (ownerLookup.TryGetComponent(current, out Owner owner))
                if (buildingLookup.HasComponent(owner.m_Owner))
                    current = owner.m_Owner;
            return current;
        }
        
        private bool IsTourist(Entity citizen)
        {
            if (householdMemberLookup.TryGetComponent(citizen, out HouseholdMember member))
            {
                return touristHouseholdLookup.HasComponent(member.m_Household);
            }
            return false;
        }
    }
}